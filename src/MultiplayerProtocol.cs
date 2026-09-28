using System.Text.Json;
using System.Text.Json.Nodes;

namespace EnhancedStorageBackpack;

// Data-only protocol: this does not authorize inventory access or write saves.
internal static class MultiplayerProtocol
{
    internal const string Build = "0.2.0-dev.5";
    internal const int Version = 1;
    internal const string Key = "EnhancedStorageBackpackNetwork";
    internal const int MaximumOfferCharacters = 8192;
    internal const int MaximumInventoryCharacters = 4 * 1024 * 1024;
    internal static readonly string[] StorageIds =
    {
        "smallstoragerack", "mediumstoragerack", "largestoragerack",
        "smallstoragecloset", "mediumstoragecloset", "largestoragecloset",
        "hugestoragecloset", "safe", "filingcabinet"
    };

    internal sealed class Offer
    {
        public int Protocol { get; set; }
        public string Build { get; set; } = "";
        public string Session { get; set; } = "";
        public int BackpackSlots { get; set; }
        public int[] Slots { get; set; } = Array.Empty<int>();
        public int[] Rows { get; set; } = Array.Empty<int>();
    }

    internal static void Validate(Offer offer)
    {
        if (offer.Protocol != Version || offer.Build != Build)
            throw new InvalidDataException("Incompatible ESB protocol/build.");
        if (!Guid.TryParseExact(offer.Session, "N", out _) || offer.BackpackSlots is < 1 or > 128 ||
            offer.Slots == null || offer.Rows == null ||
            offer.Slots.Length != StorageIds.Length || offer.Rows.Length != StorageIds.Length)
            throw new InvalidDataException("Invalid ESB host configuration.");
        if (offer.Slots.Any(value => value is < 0 or > 128) ||
            offer.Rows.Any(value => value is < 0 or > 128))
            throw new InvalidDataException("ESB storage configuration out of range.");
    }

    private static JsonObject Inventory(string inventory)
    {
        if (inventory == null || inventory.Length > MaximumInventoryCharacters)
            throw new InvalidDataException("ESB inventory envelope exceeds limit.");
        return JsonNode.Parse(inventory) as JsonObject
            ?? throw new InvalidDataException("ESB inventory envelope is not an object.");
    }

    internal static string Attach(string inventory, Offer offer)
    {
        Validate(offer);
        var root = Inventory(inventory);
        root[Key] = JsonSerializer.SerializeToNode(offer);
        string result = root.ToJsonString();
        if (result.Length > MaximumInventoryCharacters)
            throw new InvalidDataException("ESB inventory envelope exceeds limit.");
        return result;
    }

    // Strip only our transport metadata. Preserve the backpack and every vanilla
    // inventory field; incompatible offers are rejected after stripping as well.
    internal static Offer? Extract(ref string inventory)
    {
        var root = Inventory(inventory);
        if (!root.TryGetPropertyValue(Key, out var node)) return null;
        string? json = node?.ToJsonString();
        root.Remove(Key);
        inventory = root.ToJsonString();
        if (json == null || json.Length > MaximumOfferCharacters)
            throw new InvalidDataException("Invalid ESB host offer size.");
        var offer = JsonSerializer.Deserialize<Offer>(json)
            ?? throw new InvalidDataException("Missing ESB host offer.");
        Validate(offer);
        return offer;
    }
}
