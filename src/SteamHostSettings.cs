using Il2CppFishNet;
using Il2CppScheduleOne.Networking;
using Il2CppSteamworks;
using SteamNetworkLib;
using SteamNetworkLib.Core;
using SteamNetworkLib.Events;
using SteamNetworkLib.Models;
using System.Text.Json;
using UnityEngine;

namespace EnhancedStorageBackpack;

// Own channel: never consume the game's or another mod's default channels.
internal sealed class SteamHostSettings : IDisposable
{
    private const int Channel = 21837;
    private const string Key = "codexgaming.esb.host-settings.v1";
    private readonly Settings settings;
    private readonly Func<MultiplayerProtocol.Offer> getOffer;
    private readonly SteamNetworkClient network;
    private readonly Dictionary<ulong, string> peerTokens = new();
    private readonly Dictionary<(ulong Peer, string Kind), float> lastMessages = new();
    private readonly List<Task<bool>> sends = new();
    private readonly HashSet<ulong> members = new();
    private readonly Dictionary<ulong, long> accepted = new();
    private HostSettingsSession? client;
    private MultiplayerProtocol.Offer? offer;
    private string configuration = "";
    private ulong lobbyId, ownerId, localId;
    private long revision;
    private float nextInit, nextPoll;
    private bool dirty = true, reportedError, reportedWaiting;
    private float nextTrace;
    private long sentMessages, receivedMessages, completedSends, refusedSends, failedSends;

    internal SteamHostSettings(Settings settings, Func<MultiplayerProtocol.Offer> getOffer)
    {
        this.settings = settings; this.getOffer = getOffer;
        network = new SteamNetworkClient(new NetworkRules
        {
            MinReceiveChannel = Channel, MaxReceiveChannel = Channel,
            MessagePolicy = _ => (Channel, EP2PSend.k_EP2PSendReliable)
        });
        network.OnP2PMessageReceived += Received;
        settings.RackChanged += Changed;
        settings.BackpackChanged += Changed;
    }
    private void Changed() => dirty = true;
    internal void Reset()
    {
        client = null; offer = null; configuration = ""; revision = 0;
        peerTokens.Clear(); lastMessages.Clear(); members.Clear(); accepted.Clear();
        settings.SetHostConfiguration(null);
        lobbyId = ownerId = localId = 0; dirty = true; nextPoll = 0;
    }
    internal void Tick()
    {
        try
        {
            float now = Time.realtimeSinceStartup;
            if (!network.IsInitialized)
            {
                if (now < nextInit) return;
                nextInit = now + 5;
                if (!network.TryInitialize())
                {
                    if (!reportedWaiting) { reportedWaiting = true; settings.Trace("ESB_STEAM_WAIT | backend not ready; retry in five seconds"); }
                    return;
                }
                network.P2PManager!.OnSessionRequested += Admission;
                settings.Trace("ESB_STEAM_READY | SteamNetworkLib=" + SteamNetworkClient.LibraryVersion);
            }
            for (int i = sends.Count - 1; i >= 0; i--)
                if (sends[i].IsCompleted)
                {
                    var completed = sends[i]; sends.RemoveAt(i);
                    if (completed.IsFaulted) failedSends++;
                    else if (completed.IsCanceled || !completed.Result) refusedSends++;
                    else completedSends++;
                    if (completed.IsFaulted) { _ = completed.Exception; settings.Trace("ESB_STEAM_SEND | failed; retry on next handshake"); }
                    else if (!completed.IsCanceled && !completed.Result) settings.Trace("ESB_STEAM_SEND | refused; retry on next handshake");
                }
            if (now >= nextPoll)
            {
                nextPoll = now + 2;
                RefreshLobby();
                TraceHandshake(now);
                if (lobbyId != 0)
                {
                    if (localId == ownerId && InstanceFinder.IsServer)
                    {
                        var latest = getOffer();
                        string serialized = JsonSerializer.Serialize(latest);
                        if (serialized != configuration)
                        {
                            configuration = serialized; offer = latest; revision++; accepted.Clear(); dirty = true;
                        }
                        if (dirty)
                        {
                            dirty = false;
                            foreach (var peer in peerTokens.ToArray()) SendOffer(peer.Key, peer.Value);
                            settings.Trace($"ESB_HOST_SETTINGS | revision={revision} | backpack={offer?.BackpackSlots} | peers={peerTokens.Count}");
                        }
                    }
                    else if (client != null) Send(ownerId, client.Hello());
                }
            }
            network.ProcessIncomingMessages();
        }
        catch (Exception ex)
        {
            Reset();
            if (!reportedError) { reportedError = true; settings.Error("ESB_STEAM", ex); }
        }
    }
    // Temporary beta probe: once per 15 seconds, only in the dedicated debug log.
    // No Steam IDs, tokens, inventory contents or save paths are recorded.
    private void TraceHandshake(float now)
    {
        if (!settings.DebugLogging.Value || now < nextTrace) return;
        nextTrace = now + 15;
        try
        {
            bool exists = Lobby.InstanceExists;
            bool inLobby = exists && Lobby.Instance.IsInLobby;
            bool rawIdPresent = exists && Lobby.Instance.LobbyID != 0;
            settings.Trace($"ESB_HANDSHAKE_TRACE_V1 | lobbyExists={exists} | inLobby={inLobby} | rawLobbyId={rawIdPresent} | serviceLobbyId={CurrentSteamLobbyId() != 0} | lobbyId={lobbyId != 0} | ownerId={ownerId != 0} | localId={localId != 0} | localIsOwner={localId != 0 && localId == ownerId} | server={InstanceFinder.IsServer} | clientOnly={InstanceFinder.IsClientOnly} | members={members.Count} | clientSession={client != null} | ready={client?.Ready == true} | peerTokens={peerTokens.Count} | accepted={accepted.Count} | revision={revision} | sent={sentMessages} | received={receivedMessages} | completed={completedSends} | refused={refusedSends} | failed={failedSends} | pending={sends.Count}");
        }
        catch (Exception ex)
        {
            // A diagnostic probe must not reset or interrupt a live handshake.
            settings.Trace("ESB_HANDSHAKE_TRACE_V1 | probe-error=" + ex.GetType().Name);
        }
    }
    private static ulong CurrentSteamLobbyId()
    {
        // Lobby.LobbyID is an unpopulated legacy property in the current game.
        // The active Steam service owns the ID and clears it when leaving.
        if (!Lobby.InstanceExists || !Lobby.Instance.IsInLobby) return 0;
        var service = Lobby.Instance._lobbyService?.TryCast<SteamLobbyService>();
        return service != null && service.IsInLobby ? service._lobbyID : 0;
    }
    private void RefreshLobby()
    {
        ulong id = CurrentSteamLobbyId();
        ulong owner = id == 0 ? 0 : SteamMatchmaking.GetLobbyOwner(new CSteamID(id)).m_SteamID;
        ulong local = network.LocalPlayerId64;
        if (id != lobbyId || owner != ownerId || local != localId)
        {
            Reset(); lobbyId = id; ownerId = owner; localId = local;
            if (id != 0 && owner != 0 && local != 0 && local != owner)
                client = new HostSettingsSession(local, owner);
        }
        members.Clear();
        if (id == 0 || owner == 0 || local == 0) return;
        var lobby = new CSteamID(id);
        int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
        if (count > 64) throw new InvalidOperationException("Lobby exceeds ESB handshake limit.");
        for (int i = 0; i < count; i++) members.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID);
        foreach (var peer in peerTokens.Keys.Where(x => !members.Contains(x)).ToArray())
        { peerTokens.Remove(peer); accepted.Remove(peer); foreach (var key in lastMessages.Keys.Where(x => x.Peer == peer).ToArray()) lastMessages.Remove(key); }
    }
    private bool CurrentMember(ulong id)
    {
        if (lobbyId == 0 || CurrentSteamLobbyId() != lobbyId) return false;
        var lobby = new CSteamID(lobbyId);
        if (SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID != ownerId) return false;
        int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
        if (count > 64) return false;
        for (int i = 0; i < count; i++)
            if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID == id) return true;
        return false;
    }
    private void Admission(object? sender, P2PSessionRequestEventArgs e)
    {
        try { e.ShouldAccept = CurrentMember(e.RequesterId.m_SteamID); }
        catch { e.ShouldAccept = false; }
    }
    private void Received(object? sender, P2PMessageReceivedEventArgs e)
    {
        if (e.Channel != Channel || e.Message is not DataSyncMessage data || data.Key != Key) return;
        receivedMessages++;
        try
        {
            ulong from = e.SenderId.m_SteamID;
            if (from == localId || !CurrentMember(from)) return;
            var message = HostSettingsSession.Decode(data.Value);
            float now = Time.realtimeSinceStartup;
            var rateKey = (from, message.Kind);
            if (lastMessages.TryGetValue(rateKey, out float previous) && now - previous < 0.02f) return;
            lastMessages[rateKey] = now;
            if (message.Build != MultiplayerProtocol.Build) return;
            if (localId == ownerId && InstanceFinder.IsServer)
            {
                if (message.Kind == "hello")
                {
                    if (!peerTokens.TryGetValue(from, out var oldToken) || oldToken != message.ClientToken) accepted.Remove(from);
                    peerTokens[from] = message.ClientToken;
                    SendOffer(from, message.ClientToken);
                }
                else if (message.Kind == "ack" && offer != null &&
                    peerTokens.TryGetValue(from, out string? token) && token == message.ClientToken &&
                    message.Session == offer.Session && message.Revision == revision)
                {
                    accepted[from] = revision;
                    Send(from, new HostSettingsSession.Message { Kind = "ready", ClientToken = token,
                        Session = offer.Session, Revision = revision });
                    // Acknowledgement is retried; do not log every heartbeat.
                }
            }
            else if (InstanceFinder.IsClientOnly && client != null)
            {
                if (from == ownerId && message.Kind == "offer" && client.Session != null && message.Session != client.Session)
                {
                    client = new HostSettingsSession(localId, ownerId);
                    settings.SetHostConfiguration(null);
                    Send(ownerId, client.Hello());
                    return;
                }
                bool wasReady = client.Ready;
                var reply = client.Receive(from, message);
                settings.SetHostConfiguration(client.Effective);
                if (reply != null) Send(ownerId, reply);
                if (client.Ready && !wasReady) settings.Trace($"ESB_HANDSHAKE_CLIENT | revision={message.Revision} | settings=host | inventory=blocked");
            }
        }
        catch (Exception ex) { if (!reportedError) { reportedError = true; settings.Error("ESB_HANDSHAKE", ex); } }
    }
    private void SendOffer(ulong peer, string token)
    {
        if (offer != null) Send(peer, new HostSettingsSession.Message { Kind = "offer", ClientToken = token,
            Settings = offer, Session = offer.Session, Revision = revision });
    }
    private void Send(ulong peer, HostSettingsSession.Message message)
    {
        if (sends.Count >= 64 || !CurrentMember(peer)) return;
        sends.Add(network.SendMessageToPlayerAsync(new CSteamID(peer), new DataSyncMessage
            { Key = Key, Value = HostSettingsSession.Encode(message), DataType = "base64-json" }));
        sentMessages++;
    }
    internal bool TryClientContext(out string session, out string token)
    {
        session = token = "";
        if (!InstanceFinder.IsClientOnly || client?.Ready != true || !CurrentMember(ownerId)) return false;
        session = client.Session!; token = client.Hello().ClientToken;
        return true;
    }
    // Preserve authenticated transport identity across a settings re-ack. A
    // configuration refresh must not strand an inventory operation in flight.
    internal bool TryTransportContext(out string session, out string token)
    {
        session = token = "";
        if (!InstanceFinder.IsClientOnly || client?.Session == null || !CurrentMember(ownerId)) return false;
        session = client.Session; token = client.Hello().ClientToken; return true;
    }
    internal bool PeerTransportMatches(ulong peer, string session, string token) =>
        InstanceFinder.IsServer && localId == ownerId && offer?.Session == session &&
        peerTokens.TryGetValue(peer, out var expected) && expected == token && CurrentMember(peer);
    internal bool PeerContextMatches(ulong peer, string session, string token) =>
        InstanceFinder.IsServer && localId == ownerId && offer != null && offer.Session == session &&
        accepted.TryGetValue(peer, out long value) && value == revision &&
        peerTokens.TryGetValue(peer, out var expected) && expected == token && CurrentMember(peer);
    public void Dispose()
    {
        settings.RackChanged -= Changed; settings.BackpackChanged -= Changed;
        network.OnP2PMessageReceived -= Received;
        if (network.P2PManager != null) network.P2PManager.OnSessionRequested -= Admission;
        network.Dispose();
        foreach (var task in sends) _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        settings.SetHostConfiguration(null);
    }
}
