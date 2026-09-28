namespace EnhancedStorageBackpack;

// Preservation adapter while remote transactions remain disabled. Reads only
// trusted host save data, never a payload supplied by a peer. Does not write files.
internal sealed class RemoteBackpackSaves
{
    private readonly Dictionary<string, string?> payloads = new(StringComparer.Ordinal);
    internal void Reset() => payloads.Clear();
    internal bool Contains(string player) => payloads.ContainsKey(player);

    internal void Load(string player, string inventory)
    {
        if (string.IsNullOrWhiteSpace(player) || player.Length > 128)
            throw new InvalidDataException("Invalid remote save identity.");
        if (!payloads.ContainsKey(player) && payloads.Count >= 64)
            throw new InvalidDataException("Remote save cache limit reached.");
        string? payload = BackpackSave.Extract(ref inventory);
        if (payload != null) _ = BackpackSave.Decode(payload);
        if (payloads.TryGetValue(player, out string? previous))
        {
            // A disk re-read must not silently replace the session's state.
            if (previous != payload) throw new InvalidDataException("Remote backpack save changed during this session.");
            return;
        }
        payloads.Add(player, payload);
    }
    internal string Preserve(string player, string currentInventory)
    {
        if (!payloads.TryGetValue(player, out string? payload))
            throw new InvalidOperationException("Remote backpack has not been loaded from the host save.");
        // The caller supplies the host's CURRENT native hotbar. Never restore an
        // old hotbar merely to retain its backpack extension.
        string cleaned = currentInventory;
        string? embedded = BackpackSave.Extract(ref cleaned);
        if (embedded != null && embedded != payload)
            throw new InvalidDataException("Conflicting remote backpack at native save boundary.");
        return payload == null ? currentInventory : BackpackSave.Attach(cleaned, payload);
    }
}
