using HarmonyLib;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Persistence;
using UnityEngine;

namespace EnhancedStorageBackpack;

// Reuses the game's existing reliable string RPC. No custom FishNet registration,
// variable creation or item mutation. Authenticated, read-only snapshot preflight.
internal sealed class NativeInventoryChannel : IDisposable
{
    private const string Key = "codexgaming.esb.inventory-channel.v1";
    private static NativeInventoryChannel? current;
    private static (IntPtr Player, NetworkConnection? Connection) reader;
    private readonly Settings settings;
    private readonly SteamHostSettings steam;
    private readonly Dictionary<IntPtr, float> lastReplies = new();
    private readonly Dictionary<IntPtr, InventoryChannelProbe> proofs = new();
    private readonly NativeInventorySnapshots snapshots;
    private InventoryChannelProbe? pending;
    private float next;
    private int attempts;
    private bool complete, reported;
    internal NativeInventoryChannel(Settings settings, SteamHostSettings steam, HarmonyLib.Harmony harmony, Func<Player, string> readSnapshot)
    {
        this.settings = settings; this.steam = steam; current = this;
        snapshots = new NativeInventorySnapshots(settings, readSnapshot);
        harmony.Patch(AccessTools.Method(typeof(Player), "RpcReader___Server_SendValue_3589193952"),
            prefix: new HarmonyMethod(typeof(NativeInventoryChannel), nameof(ReaderStarting)),
            finalizer: new HarmonyMethod(typeof(NativeInventoryChannel), nameof(ReaderFinished)));
        harmony.Patch(AccessTools.Method(typeof(Player), "RpcLogic___SendValue_3589193952"),
            prefix: new HarmonyMethod(typeof(NativeInventoryChannel), nameof(ServerValue)));
        harmony.Patch(AccessTools.Method(typeof(Player), "RpcLogic___ReceiveValue_3895153758"),
            prefix: new HarmonyMethod(typeof(NativeInventoryChannel), nameof(ClientValue)));
    }
    private static void ReaderStarting(Player __instance, NetworkConnection __2,
        out (IntPtr Player, NetworkConnection? Connection) __state)
    { __state = reader; reader = (__instance.Pointer, __2); }
    private static Exception? ReaderFinished(Exception? __exception,
        (IntPtr Player, NetworkConnection? Connection) __state)
    { reader = __state; return __exception; }
    private static bool ServerValue(Player __instance, string __0, string __1, bool __2)
    {
        if (__0 != Key && __0 != NativeInventorySnapshots.Key) return true;
        // SendValue itself has no ownership requirement. Authenticate the actual
        // reader connection; player codes or a payload sender alone are insufficient.
        if (current == null || !InstanceFinder.IsServer || __2 || reader.Player != __instance.Pointer ||
            reader.Connection == null || __instance.Owner == null ||
            reader.Connection.ClientId != __instance.Owner.ClientId) return false;
        if (__0 == Key) current.HandleServer(__instance, __1);
        else current.HandleSnapshot(__instance, __1);
        return false;
    }
    private void HandleServer(Player player, string text)
    {
        try
        {
            var message = InventoryChannelProbe.Decode(text);
            if (message.Kind != "probe" || !ulong.TryParse(player.PlayerCode, out ulong peer) ||
                !steam.PeerContextMatches(peer, message.Session, message.Token)) return;
            float now = Time.realtimeSinceStartup;
            if (lastReplies.TryGetValue(player.Pointer, out float previous) && now - previous < 1) return;
            if (!lastReplies.ContainsKey(player.Pointer) && lastReplies.Count >= 64) return;
            lastReplies[player.Pointer] = now;
            proofs[player.Pointer] = message;
            message.Kind = "reply";
            player.ReceiveValue(player.Owner, Key, message.Encode());
            settings.Trace("ESB_NATIVE_CHANNEL_HOST | authenticated probe replied; no inventory mutation");
        }
        catch (Exception ex) { Report(ex); }
    }
    private bool SnapshotAuthenticated(Player player, InventorySnapshotWire packet)
        => player.Owner != null && proofs.TryGetValue(player.Pointer, out var proof) &&
            proof.Session == packet.Session && proof.Token == packet.Token && proof.Nonce == packet.Nonce &&
            ulong.TryParse(player.PlayerCode, out ulong peer) && steam.PeerContextMatches(peer, packet.Session, packet.Token);
    private void HandleSnapshot(Player player, string text)
    {
        try
        {
            var packet = InventorySnapshotWire.Decode(text);
            if (packet.Kind != "request" || !SnapshotAuthenticated(player, packet) ||
                !LoadManager.InstanceExists || !LoadManager.Instance.IsGameLoaded || LoadManager.Instance.IsLoading ||
                (SaveManager.InstanceExists && SaveManager.Instance.IsSaving)) return;
            snapshots.HostRequest(player, packet);
        }
        catch (Exception ex) { Report(ex); }
    }
    private static bool ClientValue(Player __instance, string __1, string __2)
    {
        if (__1 != Key && __1 != NativeInventorySnapshots.Key) return true;
        if (current == null || !InstanceFinder.IsClientOnly || !__instance.IsLocalPlayer) return false;
        try
        {
            if (__1 == NativeInventorySnapshots.Key)
            {
                var packet = InventorySnapshotWire.Decode(__2);
                if (current.complete && current.steam.TryClientContext(out string activeSession, out string activeToken) &&
                    packet.Session == activeSession && packet.Token == activeToken) current.snapshots.Receive(packet);
                return false;
            }
            var reply = InventoryChannelProbe.Decode(__2);
            if (current.pending != null && !current.complete && reply.IsReplyTo(current.pending) &&
                current.steam.TryClientContext(out string session, out string token) &&
                reply.Session == session && reply.Token == token)
            {
                current.complete = true;
                current.settings.Trace("ESB_NATIVE_CHANNEL_CLIENT | roundtrip confirmed; inventory remains blocked");
            }
        }
        catch (Exception ex) { current.Report(ex); }
        return false;
    }
    internal void Tick()
    {
        try { TickCore(); } catch (Exception ex) { Report(ex); }
    }
    private void TickCore()
    {
        if (InstanceFinder.IsServer) { snapshots.TickHost(SnapshotAuthenticated); return; }
        if (!InstanceFinder.IsClientOnly) { Reset(); return; }
        if (Player.Local == null ||
            !steam.TryClientContext(out string session, out string token)) { Reset(); return; }
        if (pending == null || pending.Session != session || pending.Token != token)
        {
            Reset(); pending = new InventoryChannelProbe { Session = session, Token = token, Nonce = Guid.NewGuid().ToString("N") };
        }
        if (complete) { snapshots.TickClient(pending); return; }
        if (attempts >= 3 || Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 5; attempts++;
        try { Player.Local.SendValue(Key, pending.Encode(), false); }
        catch (Exception ex) { Report(ex); }
        if (attempts == 3) settings.Trace("ESB_NATIVE_CHANNEL_PROBE | final attempt sent; wait for client roundtrip marker");
    }
    internal void Reset()
    { pending = null; complete = false; attempts = 0; next = 0; lastReplies.Clear(); proofs.Clear(); snapshots.Reset(); }
    private void Report(Exception ex)
    { if (!reported) { reported = true; settings.Error("ESB_NATIVE_CHANNEL", ex); } }
    public void Dispose()
    { Reset(); lastReplies.Clear(); if (ReferenceEquals(current, this)) current = null; reader = default; }
}
