using HarmonyLib;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;

namespace EnhancedStorageBackpack;

// Reuses the game's existing reliable string RPC. No custom FishNet registration,
// variable creation or item mutation. Probe only until two-peer evidence exists.
internal sealed class NativeInventoryChannel : IDisposable
{
    private const string Key = "codexgaming.esb.inventory-channel.v1";
    private static NativeInventoryChannel? current;
    private static (IntPtr Player, NetworkConnection? Connection) reader;
    private readonly Settings settings;
    private readonly SteamHostSettings steam;
    private readonly Dictionary<IntPtr, float> lastReplies = new();
    private InventoryChannelProbe? pending;
    private float next;
    private int attempts;
    private bool complete, reported;
    internal NativeInventoryChannel(Settings settings, SteamHostSettings steam, HarmonyLib.Harmony harmony)
    {
        this.settings = settings; this.steam = steam; current = this;
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
        if (__0 != Key) return true;
        // SendValue itself has no ownership requirement. Authenticate the actual
        // reader connection; player codes or a payload sender alone are insufficient.
        if (current == null || !InstanceFinder.IsServer || __2 || reader.Player != __instance.Pointer ||
            reader.Connection == null || __instance.Owner == null ||
            reader.Connection.ClientId != __instance.Owner.ClientId) return false;
        current.HandleServer(__instance, __1);
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
            message.Kind = "reply";
            player.ReceiveValue(player.Owner, Key, message.Encode());
            settings.Trace("ESB_NATIVE_CHANNEL_HOST | authenticated probe replied; no inventory mutation");
        }
        catch (Exception ex) { Report(ex); }
    }
    private static bool ClientValue(Player __instance, string __1, string __2)
    {
        if (__1 != Key) return true;
        if (current == null || !InstanceFinder.IsClientOnly || !__instance.IsLocalPlayer) return false;
        try
        {
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
        try { TickCore(); } catch (Exception ex) { Reset(); Report(ex); }
    }
    private void TickCore()
    {
        if (!InstanceFinder.IsClientOnly) return;
        if (Player.Local == null ||
            !steam.TryClientContext(out string session, out string token)) { Reset(); return; }
        if (pending == null || pending.Session != session || pending.Token != token)
        {
            Reset(); pending = new InventoryChannelProbe { Session = session, Token = token, Nonce = Guid.NewGuid().ToString("N") };
        }
        if (complete || attempts >= 3 || Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 5; attempts++;
        try { Player.Local.SendValue(Key, pending.Encode(), false); }
        catch (Exception ex) { Report(ex); }
        if (attempts == 3) settings.Trace("ESB_NATIVE_CHANNEL_PROBE | final attempt sent; wait for client roundtrip marker");
    }
    internal void Reset()
    { pending = null; complete = false; attempts = 0; next = 0; lastReplies.Clear(); }
    private void Report(Exception ex)
    { if (!reported) { reported = true; settings.Error("ESB_NATIVE_CHANNEL", ex); } }
    public void Dispose()
    { Reset(); lastReplies.Clear(); if (ReferenceEquals(current, this)) current = null; reader = default; }
}
