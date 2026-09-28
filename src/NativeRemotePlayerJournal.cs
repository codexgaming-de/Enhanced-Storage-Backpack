using HarmonyLib;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.PlayerScripts;

namespace EnhancedStorageBackpack;

// Active only for remote owners admitted by the authenticated snapshot handshake.
// The local player's stable persistence path is not intercepted by this component.
internal sealed class NativeRemotePlayerJournal : IDisposable
{
    private sealed record NativeData(Player? Player, PlayerData Data, Il2CppReferenceArray<VariableData> Variables);
    private static NativeRemotePlayerJournal? current;
    private readonly RemotePlayerJournal journal = new();
    private readonly Dictionary<string, NativeData> native = new(StringComparer.Ordinal);
    private readonly HashSet<string> restoreIssued = new(StringComparer.Ordinal);
    private readonly Settings settings;
    private readonly Func<Player, string> inventory;
    internal NativeRemotePlayerJournal(Settings settings, Func<Player, string> inventory, HarmonyLib.Harmony harmony)
    {
        this.settings = settings; this.inventory = inventory; current = this;
        Patch(harmony, typeof(PlayerManager), "SavePlayer", nameof(SavingOne));
        Patch(harmony, typeof(PlayerManager), "WriteData", nameof(SavingWorld));
        Patch(harmony, typeof(PlayerManager), "TryGetPlayerData", nameof(Restoring));
        Patch(harmony, typeof(Player), "OnStopClient", nameof(Leaving));
        Patch(harmony, typeof(Player), "PreDestroyClientObjects", nameof(ConnectionLeaving));
        harmony.Patch(AccessTools.Method(typeof(Player), "RpcLogic___SetInventoryItem_2317364410"),
            postfix: new HarmonyMethod(typeof(NativeRemotePlayerJournal), nameof(InventoryChanged)));
    }
    private static void Patch(HarmonyLib.Harmony harmony, Type type, string name, string prefix)
        => harmony.Patch(AccessTools.Method(type, name), prefix: new HarmonyMethod(typeof(NativeRemotePlayerJournal), prefix));
    private static bool HostLoaded => InstanceFinder.IsServer && LoadManager.InstanceExists &&
        LoadManager.Instance.IsGameLoaded && !LoadManager.Instance.IsLoading;
    private static bool WorldSaving => InstanceFinder.IsServer && SaveManager.InstanceExists && SaveManager.Instance.IsSaving;
    private bool Managed(Player player) => player != null && !player.IsLocalPlayer && journal.Contains(player.PlayerCode);
    internal void Admit(Player player, string currentInventory)
    {
        if (!HostLoaded || player.IsLocalPlayer) throw new InvalidOperationException("Remote host session unavailable.");
        string code = player.PlayerCode;
        if (journal.Contains(code) && !journal.IsBound(code, player.Pointer.ToInt64()))
        {
            if (!restoreIssued.Contains(code)) throw new InvalidOperationException("Live inventory was not restored before reconnect.");
            journal.Rebind(code, player.Pointer.ToInt64(), ReadNativeInventory(player));
            restoreIssued.Remove(code);
        }
        Capture(player, currentInventory);
    }
    internal static string ReadNativeInventory(Player player)
    {
        // Do not call the patched getter: during reconnect it deliberately returns
        // the cached inventory until native owner replication is complete.
        var slots = new Il2CppSystem.Collections.Generic.List<ItemSlot>();
        foreach (var slot in player._inventory) slots.Add(slot);
        return new ItemSet(slots).GetJSON();
    }
    internal bool PendingInventory(Player player, out string saved)
    {
        saved = "";
        if (!Managed(player) || (HostLoaded && journal.IsBound(player.PlayerCode, player.Pointer.ToInt64()))) return false;
        saved = journal.Read(player.PlayerCode).Inventory;
        return true;
    }
    internal bool DeferInventoryWrite(Player player)
    {
        if (!Managed(player) || WorldSaving) return false;
        if (HostLoaded && journal.IsBound(player.PlayerCode, player.Pointer.ToInt64())) Capture(player);
        settings.Trace("ESB_REMOTE_SESSION_DEFER | individual inventory file write suppressed");
        return true;
    }
    private void Capture(Player player, string? currentInventory = null)
    {
        string code = player.PlayerCode;
        var data = player.GetPlayerData();
        var variables = new List<VariableData>();
        foreach (var variable in player.PlayerVariables)
            if (variable != null && variable.Persistent)
                variables.Add(new VariableData(variable.Name, variable.GetValue().ToString()));
        var vars = new Il2CppReferenceArray<VariableData>(variables.ToArray());
        var snapshot = new RemotePlayerJournal.Snapshot(data.GetJson(), currentInventory ?? inventory(player),
            player.GetAppearanceString(), player.GetClothingString(), new VariableCollectionData(vars).GetJson());
        journal.Capture(code, player.Pointer.ToInt64(), snapshot);
        native[code] = new NativeData(player, data, vars);
    }
    private void Failure(string code, Exception error)
    {
        journal.Fault(code);
        settings.Error("ESB_REMOTE_SESSION_FAULT", error);
    }
    private bool TryBindRestored(Player player)
    {
        string code = player.PlayerCode;
        if (journal.IsBound(code, player.Pointer.ToInt64())) return true;
        if (!journal.Detached(code) || !restoreIssued.Contains(code)) return false;
        string raw = ReadNativeInventory(player);
        if (!journal.NativeMatches(code, raw)) return false;
        journal.Rebind(code, player.Pointer.ToInt64(), raw);
        restoreIssued.Remove(code);
        Capture(player);
        return true;
    }
    private static void InventoryChanged(Player __instance)
    {
        var self = current;
        if (!HostLoaded || self == null || !self.Managed(__instance)) return;
        try
        {
            // Existing managed players can rejoin without the mod: native owner
            // replication, rather than a new mod handshake, completes rebinding.
            if (!self.TryBindRestored(__instance)) return;
            // Event-driven RAM update, not per-frame polling and not a file write.
            var previous = self.journal.Read(__instance.PlayerCode);
            self.journal.Capture(__instance.PlayerCode, __instance.Pointer.ToInt64(),
                previous with { Inventory = self.inventory(__instance) });
        }
        catch (Exception ex) { self.Failure(__instance.PlayerCode, ex); }
    }
    private static bool SavingOne(Player __0)
    {
        var self = current;
        if (self == null || !self.Managed(__0)) return true;
        if (!HostLoaded) return WorldSaving; // Teardown alone must not become an implicit inventory save.
        // SavePlayer is also called on individual disconnect requests. Capture RAM
        // state but do not advance this player's disk inventory ahead of the world.
        if (!self.journal.IsBound(__0.PlayerCode, __0.Pointer.ToInt64())) return false;
        try { self.Capture(__0); }
        catch (Exception ex)
        {
            self.Failure(__0.PlayerCode, ex);
            if (SaveManager.InstanceExists && SaveManager.Instance.IsSaving)
            { SaveManager.ReportSaveError(); throw; }
            return false; // Never stall native disconnect acknowledgement.
        }
        if (SaveManager.InstanceExists && SaveManager.Instance.IsSaving) return true;
        self.settings.Trace("ESB_REMOTE_SESSION_CAPTURE | individual save retained in RAM; awaiting world save");
        return false;
    }
    private static void ConnectionLeaving(Player __instance, NetworkConnection __0)
    {
        if (__instance.Owner != null && __0 != null && __instance.Owner.ClientId == __0.ClientId)
            Leaving(__instance);
    }
    private static void Leaving(Player __instance)
    {
        var self = current;
        if (self == null || !self.Managed(__instance) ||
            !self.journal.IsBound(__instance.PlayerCode, __instance.Pointer.ToInt64())) return;
        try
        {
            if (HostLoaded) self.Capture(__instance);
            self.journal.Detach(__instance.PlayerCode, __instance.Pointer.ToInt64());
            self.restoreIssued.Remove(__instance.PlayerCode);
            self.native[__instance.PlayerCode] = self.native[__instance.PlayerCode] with { Player = null };
            self.settings.Trace("ESB_REMOTE_SESSION_LEAVE | latest coupled inventory retained in RAM");
        }
        catch (Exception ex)
        {
            // Do not interrupt native object teardown. Block later restore/save of
            // this account instead of restoring the earlier, potentially stale pair.
            self.Failure(__instance.PlayerCode, ex);
        }
    }
    private static bool Restoring(string __0, ref PlayerData __1, ref string __2,
        ref string __3, ref string __4, ref Il2CppReferenceArray<VariableData> __5, ref bool __result)
    {
        var self = current;
        if (!HostLoaded || self == null || !self.journal.Contains(__0)) return true;
        try
        {
            if (!self.journal.Detached(__0))
            {
                var active = self.native[__0].Player;
                if (active == null) throw new InvalidOperationException("Missing active remote player during restore.");
                self.Capture(active);
            }
            var saved = self.journal.Read(__0);
            var data = self.native[__0];
            __1 = data.Data; __2 = saved.Inventory; __3 = saved.Appearance;
            __4 = saved.Clothing; __5 = data.Variables; __result = true;
            self.restoreIssued.Add(__0);
            self.settings.Trace("ESB_REMOTE_SESSION_RESTORE | session inventory supplied instead of older disk state");
            return false;
        }
        catch (Exception ex) { self.Failure(__0, ex); throw; }
    }
    [HarmonyPriority(Priority.Last)]
    private static void SavingWorld(string __0)
    {
        var self = current;
        if (!WorldSaving || self == null) return;
        try
        {
            var connected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var player in Player.PlayerList)
                if (player != null)
                {
                    connected.Add(player.PlayerCode);
                    if (HostLoaded && self.Managed(player))
                    {
                        if (!self.TryBindRestored(player))
                            throw new InvalidOperationException("Remote player is still restoring; retry the world save after joining completes.");
                        self.Capture(player);
                    }
                }
            // Native PlayerManager.WriteData queues connected players only. Flush
            // detached players here, inside the same world-save operation, before
            // its directory list is collected (so native cleanup retains them).
            var offline = self.journal.OfflineSnapshots(connected);
            foreach (var pair in offline)
                RemotePlayerJournal.WriteOffline(Path.Combine(__0, "Players"), pair.Key, pair.Value);
            if (offline.Length > 0) self.settings.Trace($"ESB_REMOTE_SESSION_WORLD_SAVE | offlinePlayers={offline.Length}");
        }
        catch (Exception ex)
        {
            SaveManager.ReportSaveError();
            self.settings.Error("ESB_REMOTE_SESSION_WORLD_SAVE", ex);
            throw;
        }
    }
    internal void Reset() { journal.Reset(); native.Clear(); restoreIssued.Clear(); }
    public void Dispose() { Reset(); if (ReferenceEquals(current, this)) current = null; }
}
