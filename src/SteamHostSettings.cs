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
    private HostSettingsSession? client;
    private MultiplayerProtocol.Offer? offer;
    private string configuration = "";
    private ulong lobbyId, ownerId, localId;
    private long revision;
    private float nextInit, nextPoll;
    private bool dirty = true, reportedError, reportedWaiting;

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
        peerTokens.Clear(); lastMessages.Clear(); members.Clear();
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
                    if (completed.IsFaulted) { _ = completed.Exception; settings.Trace("ESB_STEAM_SEND | failed; retry on next handshake"); }
                    else if (!completed.IsCanceled && !completed.Result) settings.Trace("ESB_STEAM_SEND | refused; retry on next handshake");
                }
            if (now >= nextPoll)
            {
                nextPoll = now + 2;
                RefreshLobby();
                if (lobbyId != 0)
                {
                    if (localId == ownerId && InstanceFinder.IsServer)
                    {
                        var latest = getOffer();
                        string serialized = JsonSerializer.Serialize(latest);
                        if (serialized != configuration)
                        {
                            configuration = serialized; offer = latest; revision++; dirty = true;
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
            settings.SetHostConfiguration(null);
            if (!reportedError) { reportedError = true; settings.Error("ESB_STEAM", ex); }
        }
    }
    private void RefreshLobby()
    {
        ulong id = Lobby.InstanceExists && Lobby.Instance.IsInLobby ? Lobby.Instance.LobbyID : 0;
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
        { peerTokens.Remove(peer); foreach (var key in lastMessages.Keys.Where(x => x.Peer == peer).ToArray()) lastMessages.Remove(key); }
    }
    private bool CurrentMember(ulong id)
    {
        if (lobbyId == 0 || !Lobby.InstanceExists || !Lobby.Instance.IsInLobby || Lobby.Instance.LobbyID != lobbyId) return false;
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
                    peerTokens[from] = message.ClientToken;
                    SendOffer(from, message.ClientToken);
                }
                else if (message.Kind == "ack" && offer != null &&
                    peerTokens.TryGetValue(from, out string? token) && token == message.ClientToken &&
                    message.Session == offer.Session && message.Revision == revision)
                {
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
    }
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
