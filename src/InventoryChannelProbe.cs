using System.Text.Json;

namespace EnhancedStorageBackpack;

// Bounded diagnostic protocol. Carries no item data and cannot change inventories.
internal sealed class InventoryChannelProbe
{
    public string Build { get; set; } = MultiplayerProtocol.Build;
    public string Kind { get; set; } = "probe";
    public string Session { get; set; } = "";
    public string Token { get; set; } = "";
    public string Nonce { get; set; } = "";
    internal string Encode() => JsonSerializer.Serialize(this);
    internal static InventoryChannelProbe Decode(string text)
    {
        if (text == null || text.Length > 1024) throw new InvalidDataException("Invalid inventory probe size.");
        var value = JsonSerializer.Deserialize<InventoryChannelProbe>(text)
            ?? throw new InvalidDataException("Missing inventory probe.");
        if (value.Build != MultiplayerProtocol.Build || value.Kind is not ("probe" or "reply") ||
            !Guid.TryParseExact(value.Session, "N", out _) || !Guid.TryParseExact(value.Token, "N", out _) ||
            !Guid.TryParseExact(value.Nonce, "N", out _)) throw new InvalidDataException("Invalid inventory probe.");
        return value;
    }
    internal bool IsReplyTo(InventoryChannelProbe request) => Kind == "reply" &&
        Build == request.Build && Session == request.Session && Token == request.Token && Nonce == request.Nonce;
}
