using System.Text.Json;

namespace EnhancedStorageBackpack;

// Host RAM only. Capturing, leaving and reconnecting never write files.
internal sealed class RemotePlayerJournal
{
    internal sealed record Snapshot(string Player, string Inventory, string Appearance, string Clothing, string Variables);
    private sealed class Entry
    {
        internal long Binding;
        internal bool Detached, Faulted;
        internal Snapshot Latest = null!;
    }
    private int retainedCharacters;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    internal bool Contains(string code) => entries.ContainsKey(code);
    internal bool IsBound(string code, long binding) => entries.TryGetValue(code, out var e) &&
        !e.Detached && e.Binding == binding;
    internal bool Detached(string code) => entries.TryGetValue(code, out var e) && e.Detached;
    internal void Reset() { entries.Clear(); retainedCharacters = 0; }
    private static void Identity(string code)
    {
        if (!ulong.TryParse(code, out ulong id) || id == 0 || code != id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw new InvalidDataException("Invalid remote player identity.");
    }
    private static void Json(string text, bool optional = false)
    {
        if (optional && text == "") return;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1024 * 1024)
            throw new InvalidDataException("Invalid remote player snapshot size.");
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid remote player data.");
    }
    internal void Capture(string code, long binding, Snapshot snapshot)
    {
        Identity(code);
        if (binding == 0) throw new InvalidOperationException("Missing native player binding.");
        if (entries.TryGetValue(code, out var entry))
        {
            Healthy(entry);
            if (entry.Binding != binding || entry.Detached)
                throw new InvalidOperationException("Reconnect must restore the live snapshot before rebinding.");
        }
        else if (entries.Count >= 64) throw new InvalidOperationException("Remote session limit reached.");
        Json(snapshot.Player); Json(snapshot.Inventory); Json(snapshot.Appearance, true);
        Json(snapshot.Clothing); Json(snapshot.Variables);
        using (var player = JsonDocument.Parse(snapshot.Player))
            if (!player.RootElement.TryGetProperty("PlayerCode", out var value) || value.GetString() != code)
                throw new InvalidDataException("Player data identity mismatch.");
        using (var body = JsonDocument.Parse(snapshot.Inventory))
            if (!body.RootElement.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() != 9 || items.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String))
                throw new InvalidDataException("Remote inventory must include eight hotbar slots and the wallet.");
        string inventory = snapshot.Inventory;
        string? backpack = BackpackSave.Extract(ref inventory);
        if (backpack != null) _ = BackpackSave.Decode(backpack);
        int previous = entry == null ? 0 : Size(entry.Latest);
        int total = checked(retainedCharacters - previous + Size(snapshot));
        if (total > 4 * 1024 * 1024) throw new InvalidOperationException("Remote session memory budget exceeded.");
        entry ??= new Entry { Binding = binding };
        retainedCharacters = total;
        entry.Latest = snapshot; entries[code] = entry;
    }
    private static int Size(Snapshot snapshot) => checked(snapshot.Player.Length + snapshot.Inventory.Length +
        snapshot.Appearance.Length + snapshot.Clothing.Length + snapshot.Variables.Length);
    private static void Healthy(Entry entry)
    {
        if (entry.Faulted) throw new InvalidOperationException("Remote session capture failed; reload required.");
    }
    internal Snapshot Read(string code)
    { var e = entries[code]; Healthy(e); return e.Latest; }
    internal void Detach(string code, long binding)
    {
        if (entries.TryGetValue(code, out var e) && e.Binding == binding) e.Detached = true;
    }
    internal void Rebind(string code, long binding, string nativeInventory)
    {
        var e = entries[code]; Healthy(e);
        if (binding == 0 || (!e.Detached && e.Binding != binding))
            throw new InvalidOperationException("Player is already bound to another native object.");
        if (!NativeMatches(code, nativeInventory))
            throw new InvalidOperationException("Native hotbar has not restored the live session inventory yet.");
        e.Binding = binding; e.Detached = false;
    }
    internal bool NativeMatches(string code, string nativeInventory)
    {
        var e = entries[code]; Healthy(e);
        using var expected = JsonDocument.Parse(e.Latest.Inventory);
        using var actual = JsonDocument.Parse(nativeInventory);
        return actual.RootElement.TryGetProperty("Items", out var items) && items.ValueKind == JsonValueKind.Array &&
            items.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String) &&
            expected.RootElement.GetProperty("Items").EnumerateArray().Select(x => x.GetString())
                .SequenceEqual(items.EnumerateArray().Select(x => x.GetString()));
    }
    internal void Fault(string code)
    { if (entries.TryGetValue(code, out var e)) e.Faulted = true; }
    internal KeyValuePair<string, Snapshot>[] OfflineSnapshots(ISet<string> connected)
    {
        // A failed capture must never fall back to the previous (possibly duplicating) snapshot.
        foreach (var e in entries.Values) Healthy(e);
        return entries.Where(x => x.Value.Detached && !connected.Contains(x.Key))
            .Select(x => new KeyValuePair<string, Snapshot>(x.Key, x.Value.Latest)).ToArray();
    }
    internal static void WriteOffline(string playersDirectory, string code, Snapshot snapshot)
    {
        Identity(code);
        string directory = Path.Combine(playersDirectory, "Player_" + code);
        Directory.CreateDirectory(directory);
        // Inventory includes BOTH hotbar and backpack in one atomic file replacement.
        // This does not make the game's multi-file world save transactional.
        WriteAtomic(Path.Combine(directory, "Player.json"), snapshot.Player);
        WriteAtomic(Path.Combine(directory, "Clothing.json"), snapshot.Clothing);
        WriteAtomic(Path.Combine(directory, "Variables.json"), snapshot.Variables);
        if (snapshot.Appearance.Length > 0) WriteAtomic(Path.Combine(directory, "Appearance.json"), snapshot.Appearance);
        WriteAtomic(Path.Combine(directory, "Inventory.json"), snapshot.Inventory);
    }
    private static void WriteAtomic(string path, string contents)
    {
        string temporary = path + ".esb-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
