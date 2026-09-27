using System.Text.Json;
using System.Text.Json.Nodes;

namespace EnhancedStorageBackpack;

internal static class BackpackSave
{
    internal const string Key = "EnhancedStorageBackpack";
    // Embed in the very same inventory JSON returned to the game's save pipeline.
    // No file writes occur on moves, closing the menu, preference changes or exit.
    internal static string Attach(string inventory, string backpack)
    {
        var root = JsonNode.Parse(inventory) as JsonObject
            ?? throw new InvalidDataException("Player inventory is not a JSON object.");
        root[Key] = JsonNode.Parse(backpack);
        return root.ToJsonString();
    }
    internal static string? Extract(ref string inventory)
    {
        if (string.IsNullOrWhiteSpace(inventory)) return null;
        var root = JsonNode.Parse(inventory) as JsonObject
            ?? throw new InvalidDataException("Player inventory is not a JSON object.");
        if (!root.TryGetPropertyValue(Key, out var data)) return null;
        if (data == null) throw new InvalidDataException("Backpack payload is null.");
        string result = data.ToJsonString();
        root.Remove(Key);
        inventory = root.ToJsonString();
        return result;
    }
    internal static string Encode(string?[] items)
        => JsonSerializer.Serialize(new Payload { Version = 1, Items = items });
    internal static string?[] Decode(string json)
    {
        var data = JsonSerializer.Deserialize<Payload>(json);
        if (data == null || data.Version != 1 || data.Items == null || data.Items.Length > 128)
            throw new InvalidDataException("Unsupported or invalid backpack save.");
        return data.Items;
    }
    private sealed class Payload
    {
        public int Version { get; set; }
        public string?[]? Items { get; set; }
    }
}
