using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Storage;
using UnityEngine;

namespace EnhancedStorageBackpack;

internal sealed class NativeStorageSync : IDisposable
{
    private static NativeStorageSync? current;
    private readonly RackStorage storage;
    private readonly NativeInventoryChannel channel;
    private IEnumerator<Action>? outgoing;
    private readonly HashSet<IntPtr> knownPlayers = new(), knownRacks = new();
    private float nextPoll;
    private bool retryLayout;
    private readonly HashSet<IntPtr> clientLayouts = new();
    internal bool HasLayout(StorageEntity entity) => clientLayouts.Contains(entity.Pointer);
    internal void Request() => NeedsSync = true;
    internal bool NeedsSync { get; private set; }
    internal bool Sending => outgoing != null;
    internal NativeStorageSync(RackStorage storage, NativeInventoryChannel channel, HarmonyLib.Harmony harmony)
    {
        this.storage = storage; this.channel = channel; current = this;
        channel.ControlReceived += Layout;
        foreach (string method in new[] { "RpcLogic___SetStoredInstance_Internal_2652194801", "RpcLogic___SetSlotLocked_Internal_3170825843", "RpcLogic___SetSlotFilter_Internal_527532783" })
            harmony.Patch(AccessTools.Method(typeof(StorageEntity), method), prefix: new HarmonyMethod(typeof(NativeStorageSync), nameof(Indexed)));
        harmony.Patch(AccessTools.Method(typeof(StorageEntity), "RpcLogic___SetItemSlotQuantity_Internal_1692629761"),
            prefix: new HarmonyMethod(typeof(NativeStorageSync), nameof(Quantity)));
    }
    private static bool Ensure(StorageEntity entity, int index)
    {
        if (!InstanceFinder.IsClientOnly) return true;
        if (index < 0) return false;
        // Existing slots belonging to other mods are outside ESB's capacity limit.
        if (index < entity.ItemSlots.Count) return true;
        if (index >= 128) return false;
        if (index >= entity.ItemSlots.Count)
        {
            // Initial native replication may precede the ESB handshake. Only
            // allocate for supported placed storage; never index a short array.
            if (current?.storage.Supports(entity) != true) return false;
            RackStorage.Grow(entity, index + 1);
        }
        return true;
    }
    private static bool Indexed(StorageEntity __instance, int __1) => Ensure(__instance, __1);
    private static bool Quantity(StorageEntity __instance, int __0) => Ensure(__instance, __0);
    private void Layout(BackpackCommand message)
    {
        if (message.Kind != "layout" || message.Amount < 1 || message.Amount > 128 || message.Revision < 1 || message.Revision > 128) return;
        foreach (var entity in storage.Tracked)
            if (entity.ObjectId == message.From && entity.ComponentIndex == message.To)
            { storage.ReplicaLayout(entity, message.Amount, (int)message.Revision); clientLayouts.Add(entity.Pointer); return; }
        // A missing spawn is not silently accepted: the host will send the next
        // full synchronization when the client requests its backpack/open state.
        retryLayout = true;
    }
    internal void Poll()
    {
        if (Sending || Time.realtimeSinceStartup < nextPoll) return;
        if (InstanceFinder.IsClientOnly)
        {
            nextPoll = Time.realtimeSinceStartup + 2;
            if (retryLayout && channel.ClientReady && channel.ClientContext(out string session, out string token))
            {
                retryLayout = false;
                var request = new BackpackCommand { Session = session, Token = token, Nonce = Guid.NewGuid().ToString("N"), Kind = "resync" };
                Player.Local.SendValue(NativeInventoryChannel.CommandKey, request.Encode(), false);
            }
            return;
        }
        if (!InstanceFinder.IsServer) return;
        nextPoll = Time.realtimeSinceStartup + 1;
        var connected = new HashSet<IntPtr>();
        foreach (var player in Player.PlayerList)
            if (player != null && !player.IsLocalPlayer && channel.PeerReady(player))
            { connected.Add(player.Pointer); if (!knownPlayers.Contains(player.Pointer)) NeedsSync = true; }
        knownPlayers.RemoveWhere(p => !connected.Contains(p));
        foreach (var entity in storage.Tracked) if (!knownRacks.Contains(entity.Pointer)) NeedsSync = true;
    }
    internal void QueueFull()
    {
        outgoing?.Dispose(); knownPlayers.Clear(); knownRacks.Clear();
        foreach (var p in Player.PlayerList) if (p != null && !p.IsLocalPlayer && channel.PeerReady(p)) knownPlayers.Add(p.Pointer);
        foreach (var entity in storage.Tracked) knownRacks.Add(entity.Pointer);
        var targets = new List<Player>();
        foreach (var p in Player.PlayerList) if (p != null && !p.IsLocalPlayer && channel.PeerReady(p)) targets.Add(p);
        outgoing = Replicate(storage.Tracked, targets.ToArray()).GetEnumerator();
        NeedsSync = false;
    }
    private IEnumerable<Action> Replicate(StorageEntity[] entities, Player[] targets)
    {
        foreach (var entity in entities)
        {
            foreach (var player in targets)
            {
                if (player == null || player.IsLocalPlayer || !channel.PeerReady(player)) continue;
                knownPlayers.Add(player.Pointer);
                var target = player; var rack = entity;
                yield return () =>
                {
                    if (rack == null || target == null || !channel.PeerReady(target)) return;
                    var message = channel.Context(target, "layout", Guid.NewGuid().ToString("N"));
                    message.From = rack.ObjectId; message.To = rack.ComponentIndex;
                    message.Amount = rack.ItemSlots.Count; message.Revision = rack.DisplayRowCount;
                    target.ReceiveValue(target.Owner, NativeInventoryChannel.CommandKey, message.Encode());
                };
                // Explicitly include empties/unlocked/default filters. The game's
                // initial-spawn helper sends only non-default fields.
                for (int index = 0; index < rack.ItemSlots.Count; index++)
                {
                    int slotIndex = index;
                    yield return () =>
                    {
                        if (rack == null || target == null || !channel.PeerReady(target)) return;
                        var slot = rack.ItemSlots[slotIndex];
                        rack.SetStoredInstance_Internal(target.Owner, slotIndex, slot.ItemInstance);
                        rack.SetSlotFilter_Internal(target.Owner, slotIndex, slot.PlayerFilter);
                        rack.SetSlotLocked_Internal(target.Owner, slotIndex, slot.IsLocked,
                            slot.IsLocked ? slot.ActiveLock.LockOwner : null!, slot.IsLocked ? slot.ActiveLock.LockReason : "");
                    };
                }
            }
        }
    }
    internal void Drain()
    {
        for (int i = 0; i < 8 && outgoing != null; i++)
        {
            if (!outgoing.MoveNext()) { outgoing.Dispose(); outgoing = null; return; }
            outgoing.Current();
        }
    }
    internal void Reset()
    { outgoing?.Dispose(); outgoing = null; knownPlayers.Clear(); knownRacks.Clear(); NeedsSync = retryLayout = false; nextPoll = 0; clientLayouts.Clear(); }
    public void Dispose()
    { Reset(); channel.ControlReceived -= Layout; if (ReferenceEquals(current, this)) current = null; }
}
