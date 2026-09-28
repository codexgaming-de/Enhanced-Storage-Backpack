using System.Text.Json;

namespace EnhancedStorageBackpack;

// A transport-independent handshake. Only transport-authenticated identities are
// accepted as sender arguments. No message field can choose its own sender.
internal sealed class HostSettingsSession
{
    internal sealed class Message
    {
        public string Kind { get; set; } = "";
        public string Build { get; set; } = MultiplayerProtocol.Build;
        public string ClientToken { get; set; } = "";
        public long Revision { get; set; }
        public MultiplayerProtocol.Offer? Settings { get; set; }
        public string Session { get; set; } = "";
    }
    private readonly ulong local, host;
    private readonly string token = Guid.NewGuid().ToString("N");
    private MultiplayerProtocol.Offer? pending;
    private long revision;
    internal MultiplayerProtocol.Offer? Effective { get; private set; }
    internal string? Session => pending?.Session;
    internal bool Ready => Effective != null;
    internal HostSettingsSession(ulong local, ulong host)
    {
        if (local == 0 || host == 0 || local == host) throw new ArgumentException("Remote host required.");
        this.local = local; this.host = host;
    }
    internal Message Hello() => new() { Kind = "hello", ClientToken = token };
    internal Message? Receive(ulong sender, Message message)
    {
        if (sender != host || sender == local || message.Build != MultiplayerProtocol.Build || message.ClientToken != token)
            return null;
        if (message.Kind == "offer")
        {
            if (message.Settings == null || message.Revision < 1) return null;
            MultiplayerProtocol.Validate(message.Settings);
            if (message.Session != message.Settings.Session) return null;
            if (pending != null && (message.Session != pending.Session || message.Revision < revision)) return null;
            // A duplicate revision must have identical content.
            if (pending != null && message.Revision == revision &&
                JsonSerializer.Serialize(pending) != JsonSerializer.Serialize(message.Settings)) return null;
            pending = JsonSerializer.Deserialize<MultiplayerProtocol.Offer>(JsonSerializer.Serialize(message.Settings));
            revision = message.Revision;
            if (Effective != null && JsonSerializer.Serialize(Effective) != JsonSerializer.Serialize(pending)) Effective = null;
            return new Message { Kind = "ack", ClientToken = token, Session = pending!.Session, Revision = revision };
        }
        if (message.Kind == "ready" && pending != null && message.Session == pending.Session && message.Revision == revision)
            Effective = JsonSerializer.Deserialize<MultiplayerProtocol.Offer>(JsonSerializer.Serialize(pending));
        return null;
    }
    internal static string Encode(Message message) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(message));
    internal static Message Decode(string value)
    {
        if (value == null || value.Length > 16384) throw new InvalidDataException("Network message too large.");
        var message = JsonSerializer.Deserialize<Message>(Convert.FromBase64String(value))
            ?? throw new InvalidDataException("Empty network message.");
        if (!Guid.TryParseExact(message.ClientToken, "N", out _) ||
            message.Kind is not ("hello" or "offer" or "ack" or "ready"))
            throw new InvalidDataException("Invalid handshake message.");
        return message;
    }
}
