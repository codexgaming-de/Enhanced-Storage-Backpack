using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Items;
using UnityEngine;

namespace EnhancedStorageBackpack;

// All state and native calls belong to the Unity thread. A move is prepared on
// the host, mirrored by the owner, then committed after ordered native receipts.
internal sealed class RemoteBackpackRuntime : IDisposable
{
    private sealed class Peer
    {
        internal Player Player = null!;
        internal BackpackOwner Owner = null!;
        internal HostBackpackState Core = new();
        internal string Lease = "", Nonce = "", Request = "";
        internal long Sequence;
        internal int Moved;
        internal HostBackpackState.Move? Move;
        internal NativeBackpackTransfer? Transfer;
        internal NativeMoveReceipts? Receipts;
        internal string?[] Before = Array.Empty<string?>(), After = Array.Empty<string?>(), NextBackpack = Array.Empty<string?>();
        internal InventorySnapshotWire[] Reply = Array.Empty<InventorySnapshotWire>();
        internal int Sending;
        internal float Deadline;
        internal bool Faulted, Recovering;
    }
    private static RemoteBackpackRuntime? current;
    private readonly Settings settings;
    private readonly NativeInventoryChannel channel;
    private readonly Func<Player, string> read;
    private readonly Action<Player, string?[]> changed;
    private readonly Dictionary<string, Peer> peers = new(StringComparer.Ordinal);
    private readonly BackpackMenu menu;
    private readonly RackStorage storage;
    private readonly NativeStorageSync storageSync;
    private bool configurationPending, synchronizing;
    private readonly BackpackInput input = new();
    private BackpackOwner? clientOwner;
    private BackpackCommand? request;
    private InventorySnapshotReceiver? receiver;
    private string receiving = "", lease = "", appliedPlan = "", frozen = "";
    private long revision;
    private float retry, requestStarted;
    private bool applyingClient, cancelling, faulted;
    private CanvasGroup? inputGroup;
    private bool previousInteractable;
    private string?[]? plannedBefore, plannedAfter;
    private int[] plannedIndices = Array.Empty<int>();
    private string? queuedSave, barrier;
    private readonly HashSet<string> waiting = new();
    private float barrierDeadline;
    private bool executingSave, saving;
    internal bool ClientBusy => InstanceFinder.IsClientOnly && (request != null || frozen.Length != 0 || faulted);
    internal bool HostBusy => faulted || barrier != null || peers.Values.Any(p => p.Transfer != null || p.Recovering || p.Faulted);
    internal bool ClientOpen => menu.IsOpen;
    internal bool CanOpenStorage(Il2CppScheduleOne.Storage.StorageEntity entity)
        => !InstanceFinder.IsClientOnly || (!ClientBusy && channel.ClientReady && storageSync.HasLayout(entity));

    internal RemoteBackpackRuntime(Settings settings, NativeInventoryChannel channel, RackStorage storage, HarmonyLib.Harmony harmony,
        Func<Player, string> read, Action<Player, string?[]> changed)
    {
        this.settings = settings; this.channel = channel; this.read = read; this.changed = changed;
        menu = new BackpackMenu(settings) { InteractionBlocked = () => ClientBusy }; current = this;
        this.storage = storage;
        storage.DeferHostChanges = () => InstanceFinder.IsServer && RemotePlayers().Length != 0;
        settings.BackpackChanged += ConfigurationChanged;
        channel.CommandReceived = Receive; channel.ReplyReceived = Reply; channel.ControlReceived = Control;
        storageSync = new NativeStorageSync(storage, channel, harmony);
        harmony.Patch(AccessTools.Method(typeof(Player), "RpcLogic___SetInventoryItem_2317364410"),
            prefix: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(NativeReceipt)));
        foreach (string method in new[] { "SetInventoryItem", "RpcWriter___Server_SetInventoryItem_2317364410" })
            harmony.Patch(AccessTools.Method(typeof(Player), method), prefix: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(EmitInventory)));
        harmony.Patch(AccessTools.Method(typeof(ItemUIManager), "SlotClicked"), prefix: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(Click)));
        harmony.Patch(AccessTools.Method(typeof(ItemUIManager), "EndDrag"),
            prefix: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(EndDrag)),
            finalizer: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(DragFinished)));
        harmony.Patch(AccessTools.Method(typeof(PlayerInventory), "Update"), prefix: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(InventoryInput)));
        harmony.Patch(AccessTools.Method(typeof(StorageMenu), "Close"), prefix: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(CanClose)));
        harmony.Patch(AccessTools.Method(typeof(SaveManager), "Save", new[] { typeof(string) }),
            prefix: new HarmonyMethod(typeof(RemoteBackpackRuntime), nameof(SaveStarting)));
    }
    private void ConfigurationChanged() => configurationPending = true;
    private static string? Json(ItemSlot slot) => slot.ItemInstance?.GetItemData().GetJson(false);
    private static string? Json(ItemInstance? item) => item?.GetItemData().GetJson(false);
    private static ItemSlot[] Inventory(Player player) => player._inventory.ToArray();
    private static ItemSlot[] Slots(BackpackOwner owner) => owner.ItemSlots.ToArray();
    private static ItemInstance? Load(string? text)
    {
        if (text == null) return null;
        using var doc = JsonDocument.Parse(text);
        string type = doc.RootElement.GetProperty("DataType").GetString() ?? throw new InvalidDataException("Missing item type.");
        return LoadManager.Instance.GetItemLoader(type)?.LoadItem(text) ?? throw new InvalidDataException("Item loader failed.");
    }
    private static BackpackOwner Restore(string?[] values)
    {
        if (values.Length < 1 || values.Length > 128) throw new InvalidDataException("Invalid backpack capacity.");
        var items = values.Select(Load).ToArray(); // Validate all before exposing the new owner.
        var owner = new BackpackOwner();
        var native = owner.Cast<IItemSlotOwner>();
        for (int i = 0; i < items.Length; i++)
        {
            var slot = new ItemSlot(false);
            int before = owner.ItemSlots.Count;
            slot.SetSlotOwner(native);
            if (owner.ItemSlots.Count == before) owner.ItemSlots.Add(slot);
            if (owner.ItemSlots.Count != before + 1 || owner.ItemSlots[i].Pointer != slot.Pointer)
                throw new InvalidOperationException("Unexpected slot registration.");
            slot.SetStoredItem(items[i]!, true);
        }
        return owner;
    }
    private Peer Admit(Player player)
    {
        if (peers.TryGetValue(player.PlayerCode, out var existing) && existing.Player.Pointer == player.Pointer) return existing;
        string inventory = read(player);
        string? payload = BackpackSave.Extract(ref inventory);
        var contents = payload == null ? Array.Empty<string?>() : BackpackSave.Decode(payload);
        if (contents.Length == 0) contents = new string?[Math.Clamp(settings.EffectiveBackpackSlots, 1, 128)];
        var peer = new Peer { Player = player, Owner = Restore(contents) };
        ulong id = ulong.Parse(player.PlayerCode);
        peer.Core.Register(id, Inventory(player).Select(Json).ToArray(), contents);
        peer.Lease = peer.Core.Connect(id);
        peers[player.PlayerCode] = peer;
        return peer;
    }
    private void Receive(Player player, BackpackCommand command)
    {
        if (command.Kind == "resync") { storageSync.Request(); return; }
        if (command.Kind == "frozen")
        { if (command.Nonce == barrier) waiting.Remove(player.PlayerCode); return; }
        if (command.Kind is not ("open" or "move" or "commit" or "abort" or "recovered") || !Loaded) return;
        var peer = Admit(player);
        if (peer.Faulted) return;
        if (command.Nonce == peer.Nonce)
        {
            if (command.Kind == "recovered") { peer.Recovering = false; return; }
            if (command.Kind == "commit") { if (peer.Receipts?.Complete == true) Commit(peer); return; }
            if (command.Kind == "abort") { if (peer.Transfer != null) Abort(peer); return; }
            if (command.Encode() == peer.Request) peer.Sending = 0;
            return;
        }
        if (peer.Transfer != null || peer.Recovering) return;
        peer.Nonce = command.Nonce; peer.Request = command.Encode();
        ulong id = ulong.Parse(player.PlayerCode);
        peer.Core.RefreshNative(id, Inventory(player).Select(Json).ToArray(), Slots(peer.Owner).Select(Json).ToArray());
        if (barrier != null || SaveManager.Instance.IsSaving)
        { Send(peer, command, "state"); return; }
        if (command.Kind == "open")
        {
            peer.Lease = peer.Core.Connect(id); peer.Sequence = 0;
            Resize(peer);
            Send(peer, command, "state"); return;
        }
        var snapshot = peer.Core.Read(id);
        if (command.Lease != peer.Lease || command.Revision != snapshot.Revision)
        { Send(peer, command, "state"); return; }
        var from = command.FromBackpack ? Slots(peer.Owner) : Inventory(player);
        var to = command.ToBackpack ? Slots(peer.Owner) : Inventory(player);
        if (command.From >= from.Length || command.To >= to.Length) { Send(peer, command, "state"); return; }
        var transfer = new NativeBackpackTransfer(from[command.From], to[command.To], command.Amount, command.CashAmount,
            !command.FromBackpack && command.From == 8, !command.ToBackpack && command.To == 8);
        if (transfer.Plan.Kind == BackpackTransferRules.Kind.Reject) { Send(peer, command, "state"); return; }
        peer.Before = snapshot.Inventory;
        peer.After = (string?[])snapshot.Inventory.Clone(); peer.NextBackpack = snapshot.Backpack;
        (command.FromBackpack ? peer.NextBackpack : peer.After)[command.From] = transfer.NextSource;
        (command.ToBackpack ? peer.NextBackpack : peer.After)[command.To] = transfer.NextTarget;
        peer.Move = new HostBackpackState.Move(peer.Core.Session, peer.Lease, peer.Sequence + 1, snapshot.Revision,
            command.FromBackpack ? HostBackpackState.Area.Backpack : HostBackpackState.Area.Inventory, command.From,
            command.ToBackpack ? HostBackpackState.Area.Backpack : HostBackpackState.Area.Inventory, command.To, command.Amount);
        peer.Receipts = new NativeMoveReceipts(peer.Before, peer.After);
        peer.Moved = transfer.Plan.Amount;
        peer.Transfer = transfer; peer.Deadline = Time.realtimeSinceStartup + 25;
        Send(peer, command, "plan");
    }
    private void Resize(Peer peer)
    {
        ulong id = ulong.Parse(peer.Player.PlayerCode);
        peer.Core.ResizeBackpack(id, Math.Clamp(settings.EffectiveBackpackSlots, 1, 128));
        var snapshot = peer.Core.Read(id);
        peer.Owner = Restore(snapshot.Backpack);
        changed(peer.Player, snapshot.Backpack);
    }
    private void Send(Peer peer, BackpackCommand command, string kind)
    {
        // Reuse bounded chunking and integrity checks; additional metadata lives
        // beside the native Items array and never enters the game's item loader.
        var snapshot = peer.Core.Read(ulong.Parse(peer.Player.PlayerCode));
        var root = JsonNode.Parse(NativeRemotePlayerJournal.ReadNativeInventory(peer.Player))!.AsObject();
        root[BackpackSave.Key] = JsonNode.Parse(BackpackSave.Encode(snapshot.Backpack));
        root["ESBMove"] = JsonSerializer.SerializeToNode(new ReplyData
        {
            Kind = kind, Lease = peer.Lease, Revision = snapshot.Revision, Moved = peer.Moved,
            Before = kind == "plan" ? peer.Before : null,
            After = kind == "plan" ? peer.After : null,
            Indices = peer.Receipts?.Indices ?? Array.Empty<int>()
        });
        var probe = new InventoryChannelProbe { Session = command.Session, Token = command.Token, Nonce = command.Nonce };
        peer.Reply = InventorySnapshotWire.Split(InventorySnapshotWire.Request(probe), root.ToJsonString());
        peer.Sending = 0;
    }
    private sealed class ReplyData
    {
        public string Kind { get; set; } = "";
        public string Lease { get; set; } = "";
        public long Revision { get; set; }
        public int Moved { get; set; }
        public string?[]? Before { get; set; }
        public string?[]? After { get; set; }
        public int[] Indices { get; set; } = Array.Empty<int>();
    }
    private void Commit(Peer peer)
    {
        if (peer.Transfer == null || peer.Move == null || peer.Receipts?.Complete != true) return;
        try
        {
            var transfer = peer.Transfer;
            // Unrelated vanilla changes must survive this move. Refreshing would
            // invalidate its revision, so copy their current values into the core
            // through a new trusted request before the actual commit.
            ulong id = ulong.Parse(peer.Player.PlayerCode);
            peer.Core.RefreshNative(id, Inventory(peer.Player).Select(Json).ToArray(), Slots(peer.Owner).Select(Json).ToArray());
            var before = peer.Core.Read(id);
            var move = peer.Move with { Revision = before.Revision };
            var after = (string?[])before.Inventory.Clone();
            foreach (int i in peer.Receipts.Indices) after[i] = peer.After[i];
            var result = peer.Core.ApplyNative(id, move, _ => new HostBackpackState.NativeChange(after, peer.NextBackpack, transfer.Apply));
            if (result != HostBackpackState.Outcome.Applied) throw new InvalidOperationException("Host commit refused: " + result);
            peer.Sequence++;
            changed(peer.Player, peer.Core.Read(id).Backpack);
            peer.Transfer = null;
            Send(peer, BackpackCommand.Decode(peer.Request), "done");
            settings.Trace("ESB_REMOTE_MOVE_COMMITTED | coupled host inventory updated in RAM");
        }
        catch (Exception ex)
        {
            peer.Faulted = true;
            settings.Error("ESB_REMOTE_MOVE_FAULT", ex);
            SaveManager.ReportSaveError();
        }
    }
    private void Abort(Peer peer)
    {
        peer.Transfer = null; peer.Recovering = true;
        // Host slots were not changed: return an authoritative recovery snapshot.
        peer.Core.RefreshNative(ulong.Parse(peer.Player.PlayerCode), Inventory(peer.Player).Select(Json).ToArray(), Slots(peer.Owner).Select(Json).ToArray());
        Send(peer, BackpackCommand.Decode(peer.Request), "abort");
    }
    private static bool NativeReceipt(Player __instance, int __0, ItemInstance __1)
    {
        var self = current;
        if (self == null || !InstanceFinder.IsServer || __instance.IsLocalPlayer ||
            !self.peers.TryGetValue(__instance.PlayerCode, out var peer) || peer.Player.Pointer != __instance.Pointer) return true;
        if (peer.Faulted) return false;
        if (peer.Recovering && peer.Receipts?.Contains(__0) == true) return false;
        if (peer.Transfer == null || peer.Receipts == null || !peer.Receipts.Contains(__0)) return true;
        if (!peer.Receipts.Accept(__0, Json(__1)))
        {
            __instance._inventory[__0].SetStoredItem(__1, true);
            self.Abort(peer); return false;
        }
        if (peer.Receipts.Complete) self.Commit(peer);
        return false;
    }
    private static bool EmitInventory() => current?.applyingClient != true;
    private static bool InventoryInput() => current?.ClientBusy != true;
    private static bool CanClose() => current?.ClientBusy != true || current.cancelling;
    private void Begin(BackpackCommand command)
    {
        if (!channel.ClientContext(out string session, out string token)) return;
        command.Session = session; command.Token = token; command.Nonce = Guid.NewGuid().ToString("N");
        request = command; receiver = null; receiving = ""; appliedPlan = "";
        UpdateInputGate();
        plannedBefore = plannedAfter = null; plannedIndices = Array.Empty<int>();
        Player.Local.SendValue(NativeInventoryChannel.CommandKey, command.Encode(), false);
        requestStarted = Time.realtimeSinceStartup;
        retry = Time.realtimeSinceStartup + 5;
    }
    private ItemSlot[] LocalSlots()
    {
        var inventory = PlayerInventory.Instance;
        var slots = new ItemSlot[9];
        for (int i = 0; i < 8; i++) slots[i] = inventory.hotbarSlots[i].Cast<ItemSlot>();
        slots[8] = inventory.cashSlot.Cast<ItemSlot>();
        return slots;
    }
    private void Reply(InventorySnapshotWire packet)
    {
        if (request == null || packet.Nonce != request.Nonce) return;
        try
        {
            if (receiver == null || receiving != packet.Snapshot)
            {
                receiving = packet.Snapshot;
                receiver = new InventorySnapshotReceiver(InventorySnapshotWire.Request(new InventoryChannelProbe
                    { Session = request.Session, Token = request.Token, Nonce = request.Nonce }));
            }
            if (!receiver.Accept(packet)) return;
            string inventory = receiver.Inventory!;
            using var doc = JsonDocument.Parse(inventory);
            var info = doc.RootElement.GetProperty("ESBMove").Deserialize<ReplyData>() ?? throw new InvalidDataException("Missing reply metadata.");
            if (info.Kind == "plan")
            {
                if (appliedPlan == packet.Nonce) { SendReceipts(); return; }
                if (info.Before?.Length != 9 || info.After?.Length != 9 || info.Indices.Length > 2 ||
                    info.Indices.Distinct().Count() != info.Indices.Length || info.Indices.Any(i => i < 0 || i >= 9))
                    throw new InvalidDataException("Invalid transfer plan.");
                var local = LocalSlots();
                if (info.Indices.Any(i => Json(local[i]) != info.Before[i]))
                { SendControl("abort"); return; }
                plannedBefore = info.Before; plannedAfter = info.After; plannedIndices = info.Indices;
                ApplyLocal(plannedAfter, plannedIndices);
                appliedPlan = packet.Nonce;
                SendReceipts(); return;
            }
            if (info.Kind is not ("state" or "done" or "abort")) throw new InvalidDataException("Unknown transfer reply.");
            if (info.Kind == "abort" && plannedBefore != null)
            {
                // Recovery uses host current values, not a stale local before-image.
                var values = doc.RootElement.GetProperty("Items").EnumerateArray().Select(v => v.GetString()).ToArray();
                foreach (int i in plannedIndices)
                {
                    using var item = JsonDocument.Parse(values[i]!);
                    if (item.RootElement.TryGetProperty("ID", out var id) && id.GetString() == "") values[i] = null;
                }
                ApplyLocal(values, plannedIndices);
            }
            if (info.Kind == "abort") SendControl("recovered");
            string? payload = BackpackSave.Extract(ref inventory);
            var owner = Restore(payload == null ? new string?[settings.EffectiveBackpackSlots] : BackpackSave.Decode(payload));
            bool wasOpen = menu.IsOpen;
            clientOwner = owner; lease = info.Lease; revision = info.Revision;
            request = null; receiver = null; UpdateInputGate();
            if (wasOpen) menu.ReplaceOwner(owner);
            else if (frozen.Length == 0 && info.Kind == "state") menu.Open(owner);
            if (quickRemaining > 0)
            {
                if (info.Kind == "done" && info.Moved > 0 && frozen.Length == 0)
                {
                    quickRemaining -= info.Moved;
                    if (quickRemaining > 0) ContinueQuick();
                }
                else quickRemaining = 0;
            }
        }
        catch (Exception ex) { faulted = true; settings.Error("ESB_REMOTE_CLIENT_FAULT", ex); }
    }
    private void ApplyLocal(string?[] items, int[] indices)
    {
        var loaded = indices.ToDictionary(i => i, i => Load(items[i]));
        applyingClient = true;
        try
        {
            var local = LocalSlots();
            foreach (int i in indices)
            {
                if (i == 8)
                {
                    var cash = loaded[i]?.TryCast<CashInstance>() ?? throw new InvalidDataException("Wallet requires cash data.");
                    PlayerInventory.Instance.cashInstance.SetBalance(cash.Balance);
                }
                else local[i].SetStoredItem(loaded[i]!, true);
                Player.Local._inventory[i].SetStoredItem(loaded[i]?.GetCopy()!, true);
            }
            if (indices.Any(i => Json(local[i]) != items[i])) throw new InvalidOperationException("Local native mirror mismatch.");
        }
        finally { applyingClient = false; }
    }
    private void SendReceipts()
    {
        if (plannedAfter == null) return;
        foreach (int i in plannedIndices) Player.Local.SetInventoryItem(i, Load(plannedAfter[i])!);
        SendControl("commit");
    }
    private void SendControl(string kind)
    {
        if (request == null) return;
        var control = new BackpackCommand { Session = request.Session, Token = request.Token, Nonce = request.Nonce, Kind = kind };
        Player.Local.SendValue(NativeInventoryChannel.CommandKey, control.Encode(), false);
    }
    private bool Address(ItemSlot? slot, out bool backpack, out int index)
    {
        backpack = false; index = -1;
        if (slot == null) return false;
        if (clientOwner != null)
            for (int i = 0; i < clientOwner.ItemSlots.Count; i++)
                if (clientOwner.ItemSlots[i].Pointer == slot.Pointer) { backpack = true; index = i; return true; }
        var local = LocalSlots();
        for (int i = 0; i < 9; i++) if (local[i].Pointer == slot.Pointer) { index = i; return true; }
        return false;
    }
    private void Move(ItemSlot? from, ItemSlot? to, int amount, float cashAmount = 0)
    {
        if (ClientBusy || !Address(from, out bool a, out int i) || !Address(to, out bool b, out int j) ||
            (!a && !b) || (a == b && i == j) || amount < 1) return;
        Begin(new BackpackCommand { Kind = "move", Lease = lease, Revision = revision,
            FromBackpack = a, From = i, ToBackpack = b, To = j, Amount = amount, CashAmount = cashAmount });
    }
    private bool quickBackpack;
    private int quickSlot, quickRemaining;
    private void ContinueQuick()
    {
        if (clientOwner == null || !menu.IsOpen || quickRemaining <= 0) return;
        var source = quickBackpack ? clientOwner.ItemSlots[quickSlot] : LocalSlots()[quickSlot];
        if (source.ItemInstance == null) { quickRemaining = 0; return; }
        ItemSlot? target = null;
        var targets = ItemUIManager.Instance.GetQuickMoveSlots(source);
        foreach (var candidate in targets)
            if (candidate.ItemInstance != null && candidate.ItemInstance.CanStackWith(source.ItemInstance, false) && candidate.GetCapacityForItem(source.ItemInstance, true) > 0)
            { target = candidate; break; }
        if (target == null) foreach (var candidate in targets) if (candidate.ItemInstance == null) { target = candidate; break; }
        if (target == null) { quickRemaining = 0; return; }
        Move(source, target, Math.Min(quickRemaining, source.Quantity));
    }
    private static bool Click(ItemUIManager __instance, ItemSlotUI __0)
    {
        var self = current;
        if (self == null || !InstanceFinder.IsClientOnly) return true;
        if (self.ClientBusy) return false;
        if (!self.menu.IsOpen) return true;
        var source = __0?.assignedSlot;
        if (Input.GetKey(KeyCode.LeftShift) || __instance.canControllerQuickMove)
        {
            if (source?.ItemInstance == null) return false;
            var targets = __instance.GetQuickMoveSlots(source);
            ItemSlot? target = null;
            foreach (var candidate in targets)
                if (candidate.ItemInstance != null && candidate.ItemInstance.CanStackWith(source.ItemInstance, false) && candidate.GetCapacityForItem(source.ItemInstance, true) > 0)
                { target = candidate; break; }
            if (target == null) foreach (var candidate in targets) if (candidate.ItemInstance == null) { target = candidate; break; }
            int amount = __instance.controllerQuickMoveSingle || Il2CppScheduleOne.GameInput.GetButtonDown(Il2CppScheduleOne.GameInput.ButtonCode.SecondaryClick) ? 1 : source.Quantity;
            var cash = source.ItemInstance.TryCast<CashInstance>();
            if (cash != null && self.Address(source, out bool sourceBackpack, out _) && sourceBackpack)
                target = PlayerInventory.Instance.cashSlot.Cast<ItemSlot>();
            if (cash == null && self.Address(source, out bool fromBackpack, out int fromSlot))
            { self.quickBackpack = fromBackpack; self.quickSlot = fromSlot; self.quickRemaining = amount; }
            bool single = __instance.controllerQuickMoveSingle || Il2CppScheduleOne.GameInput.GetButtonDown(Il2CppScheduleOne.GameInput.ButtonCode.SecondaryClick);
            self.Move(source, target, cash != null ? 1 : amount,
                cash == null ? 0 : Math.Min(cash.Balance, single ? 100 : 1000));
            if (self.request == null) self.quickRemaining = 0;
            return false;
        }
        return true;
    }
    private static void EndDrag(ItemUIManager __instance, out ItemSlotUI? __state)
    {
        __state = null;
        var self = current;
        if (self == null || !InstanceFinder.IsClientOnly || (!self.menu.IsOpen && !self.ClientBusy && !self.cancelling)) return;
        __state = __instance.HoveredSlot;
        if (!self.cancelling && !self.ClientBusy && self.menu.IsOpen)
        {
            var target = __instance.HoveredSlot?.assignedSlot;
            if (__instance.IsDraggingCash && target?.TryCast<HotbarSlot>() != null)
                target = PlayerInventory.Instance.cashSlot.Cast<ItemSlot>();
            self.Move(__instance.draggedSlot?.assignedSlot, target, __instance.draggedAmount,
                __instance.IsDraggingCash ? __instance.draggedCashAmount : 0);
        }
        __instance.HoveredSlot = null!; // Native cleanup still runs, without mutation.
    }
    private static Exception? DragFinished(ItemUIManager __instance, ItemSlotUI? __state, Exception? __exception)
    { if (__state != null) __instance.HoveredSlot = __state; return __exception; }
    private static Player[] RemotePlayers()
    {
        var players = new List<Player>();
        foreach (var p in Player.PlayerList) if (p != null && !p.IsLocalPlayer) players.Add(p);
        return players.ToArray();
    }
    private static bool Loaded => LoadManager.InstanceExists && LoadManager.Instance.IsGameLoaded && !LoadManager.Instance.IsLoading;
    private void CancelDrag()
    {
        bool previous = cancelling;
        cancelling = true;
        try { if (ItemUIManager.InstanceExists && RackStorage.Dragging) ItemUIManager.Instance.EndDrag(); }
        finally { cancelling = previous; }
    }
    private void Control(BackpackCommand command)
    {
        if (command.Kind == "freeze")
        {
            frozen = command.Nonce; CancelDrag(); UpdateInputGate();
            if (request == null) AcknowledgeFreeze(command);
        }
        else if (command.Kind == "release" && command.Nonce == frozen)
        {
            frozen = freezeAcknowledged = ""; UpdateInputGate();
            if (menu.IsOpen && request == null)
            { menu.Close(); Begin(new BackpackCommand { Kind = "open" }); }
        }
    }
    private void UpdateInputGate()
    {
        if (!ClientBusy)
        {
            if (inputGroup != null) inputGroup.interactable = previousInteractable;
            inputGroup = null; return;
        }
        if (inputGroup != null || !StorageMenu.InstanceExists || !StorageMenu.Instance.IsOpen) return;
        inputGroup = StorageMenu.Instance.Container.GetComponent<CanvasGroup>();
        if (inputGroup == null) inputGroup = StorageMenu.Instance.Container.gameObject.AddComponent<CanvasGroup>();
        previousInteractable = inputGroup.interactable;
        inputGroup.interactable = false;
    }
    private void AcknowledgeFreeze(BackpackCommand command)
    { command.Kind = "frozen"; Player.Local.SendValue(NativeInventoryChannel.CommandKey, command.Encode(), false); }
    private static bool SaveStarting(string __0)
    {
        var self = current;
        if (self == null || !InstanceFinder.IsServer || self.executingSave || !Loaded || SaveManager.Instance.IsSaving) return true;
        var remote = RemotePlayers();
        if (remote.Length == 0 && !self.HostBusy) return true;
        if (self.queuedSave == null)
        {
            self.queuedSave = __0;
            self.barrierDeadline = Time.realtimeSinceStartup + 30;
            self.settings.Trace("ESB_SAVE_BARRIER | waiting for ordered inventory acknowledgements");
        }
        return false;
    }
    private void SaveTick()
    {
        if (faulted) return;
        if (saving)
        {
            if (!SaveManager.Instance.IsSaving) { saving = false; Release(); }
            return;
        }
        if (synchronizing)
        {
            storageSync.Drain();
            if (storageSync.Sending) return;
            synchronizing = false;
            if (queuedSave == null) { Release(); return; }
        }
        bool configure = storage.NeedsApply || configurationPending || storageSync.NeedsSync;
        if (queuedSave == null && !configure && barrier == null) return;
        if (barrier == null && queuedSave == null) barrierDeadline = Time.realtimeSinceStartup + 30;
        if (Time.realtimeSinceStartup > barrierDeadline || peers.Values.Any(p => p.Faulted))
        { SaveManager.ReportSaveError(); settings.Error("ESB_SAVE_BARRIER_FAILED", new InvalidOperationException(settings.Text("Inventar nicht synchronisiert. Speichern wurde nicht gestartet.", "Inventory not synchronized. Save was not started."))); queuedSave = null; Release(); return; }
        if (peers.Values.Any(p => p.Transfer != null || p.Recovering) || RackStorage.Dragging) return;
        var remote = RemotePlayers();
        if (remote.Any(p => !channel.PeerReady(p))) return;
        if (barrier == null)
        {
            barrier = Guid.NewGuid().ToString("N");
            foreach (var player in remote)
            {
                waiting.Add(player.PlayerCode);
                player.ReceiveValue(player.Owner, NativeInventoryChannel.CommandKey, channel.Context(player, "freeze", barrier).Encode());
            }
        }
        waiting.RemoveWhere(code => !remote.Any(p => p.PlayerCode == code));
        if (waiting.Count != 0) return;
        if (configure)
        {
            storage.ApplyPending();
            foreach (var peer in peers.Values)
                if (peer.Player != null && channel.PeerReady(peer.Player) && !peer.Faulted) Resize(peer);
            configurationPending = false;
            storageSync.QueueFull(); synchronizing = true;
            return;
        }
        if (queuedSave == null) { Release(); return; }
        var path = queuedSave; queuedSave = null;
        executingSave = true;
        try { SaveManager.Instance.Save(path); saving = SaveManager.Instance.IsSaving; }
        finally { executingSave = false; if (!saving) Release(); }
    }
    private void Release()
    {
        if (barrier != null)
            foreach (var player in Player.PlayerList)
                if (player != null && !player.IsLocalPlayer && channel.PeerReady(player))
                    player.ReceiveValue(player.Owner, NativeInventoryChannel.CommandKey, channel.Context(player, "release", barrier).Encode());
        barrier = null; waiting.Clear();
    }
    internal void Tick()
    {
        if (!Loaded) { if (clientOwner != null || peers.Count != 0 || request != null) Reset(); return; }
        try
        {
            if (InstanceFinder.IsServer)
            {
                var connected = RemotePlayers().Select(p => p.Pointer).ToHashSet();
                foreach (var peer in peers.Values.ToArray())
                {
                    if (peer.Player == null || !connected.Contains(peer.Player.Pointer))
                    { peer.Transfer = null; peer.Recovering = false; continue; }
                    if (!channel.PeerReady(peer.Player)) continue;
                    if (peer.Transfer != null && Time.realtimeSinceStartup > peer.Deadline) Abort(peer);
                    for (int n = 0; n < 2 && peer.Sending < peer.Reply.Length; n++)
                        peer.Player.ReceiveValue(peer.Player.Owner, NativeInventoryChannel.ReplyKey, peer.Reply[peer.Sending++].Encode());
                }
                storageSync.Poll();
                SaveTick(); return;
            }
            if (!InstanceFinder.IsClientOnly || Player.Local == null) return;
            storageSync.Poll();
            if (!channel.ClientReady) return;
            UpdateInputGate();
            menu.LayoutTick();
            if (menu.IsOpen && StorageMenu.InstanceExists)
                StorageMenu.Instance.SubtitleLabel.text = faulted
                    ? settings.Text("Synchronisierung fehlgeschlagen – Spielstand neu laden.", "Synchronization failed – reload the save.")
                    : ClientBusy ? settings.Text("Warte auf Host …", "Waiting for host …") : "";
            if (frozen.Length != 0 && freezeAcknowledged != frozen && request == null && channel.ClientContext(out string s, out string t))
            {
                AcknowledgeFreeze(new BackpackCommand { Session = s, Token = t, Nonce = frozen, Kind = "frozen" });
                // Keep the freeze until the explicit host release, but acknowledge once.
                freezeAcknowledged = frozen;
            }
            if (!faulted && request != null && Time.realtimeSinceStartup - requestStarted > 35)
            {
                faulted = true;
                settings.Error("ESB_REMOTE_TIMEOUT", new TimeoutException(settings.Text("Keine sichere Host-Bestätigung. Spielstand neu laden.", "No safe host acknowledgement. Reload the save.")));
            }
            if (!faulted && request != null && Time.realtimeSinceStartup >= retry)
            { retry = Time.realtimeSinceStartup + 5; Player.Local.SendValue(NativeInventoryChannel.CommandKey, request.Encode(), false); }
            if (ClientBusy || Il2CppScheduleOne.GameInput.IsTyping || !input.Pressed(settings.BackpackHotkey.Value) || RackStorage.Dragging) return;
            if (menu.IsOpen) { menu.Close(); return; }
            var player = Player.Local;
            if (Cursor.visible || player.IsArrested || player.IsUnconscious || player.IsSleeping || player.IsTased ||
                player.IsRagdolled || player.IsInVehicle || !StorageMenu.InstanceExists || StorageMenu.Instance.IsOpen) return;
            Begin(new BackpackCommand { Kind = "open" });
        }
        catch (Exception ex) { faulted = true; settings.Error("ESB_REMOTE_RUNTIME", ex); }
    }
    private string freezeAcknowledged = "";
    internal void Closed() => menu.Closed();
    internal void Reset()
    {
        cancelling = true;
        try { CancelDrag(); menu.Close(); } finally { cancelling = false; }
        if (inputGroup != null) inputGroup.interactable = previousInteractable;
        inputGroup = null;
        clientOwner = null; request = null; receiver = null; peers.Clear(); quickRemaining = 0;
        lease = receiving = appliedPlan = frozen = freezeAcknowledged = ""; faulted = false;
        queuedSave = barrier = null; waiting.Clear(); saving = synchronizing = configurationPending = false;
        storageSync.Reset();
    }
    public void Dispose()
    {
        Reset(); menu.Dispose(); storageSync.Dispose(); settings.BackpackChanged -= ConfigurationChanged;
        storage.DeferHostChanges = null; channel.CommandReceived = null; channel.ReplyReceived = null; channel.ControlReceived = null;
        if (ReferenceEquals(current, this)) current = null;
    }
}
