using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EnhancedStorageBackpack;

// Read-only opening snapshot. No client-supplied item data is accepted by the host.
// A complete, validated snapshot is a prerequisite, not permission to mutate slots.
internal sealed class InventorySnapshotWire
{
    internal const int ChunkSize = 4096, MaxChunks = 64, MaxPayload = ChunkSize * MaxChunks;
    public string Build { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Session { get; set; } = "";
    public string Token { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Snapshot { get; set; } = "";
    public string Hash { get; set; } = "";
    public int Index { get; set; }
    public int Count { get; set; }
    public string Data { get; set; } = "";

    internal static InventorySnapshotWire Request(InventoryChannelProbe probe) => new()
    { Build = MultiplayerProtocol.Build, Kind = "request", Session = probe.Session, Token = probe.Token, Nonce = probe.Nonce };
    internal bool Matches(InventorySnapshotWire other) => Build == other.Build && Session == other.Session &&
        Token == other.Token && Nonce == other.Nonce;
    internal string Encode() { Validate(); return JsonSerializer.Serialize(this); }
    internal static InventorySnapshotWire Decode(string text)
    {
        // Escaped JSON can occupy six characters per original UTF-16 code unit.
        if (text == null || text.Length > ChunkSize * 6 + 2048)
            throw new InvalidDataException("Invalid snapshot packet size.");
        var result = JsonSerializer.Deserialize<InventorySnapshotWire>(text)
            ?? throw new InvalidDataException("Missing snapshot packet.");
        result.Validate(); return result;
    }
    private void Validate()
    {
        if (Build != MultiplayerProtocol.Build || !Guid.TryParseExact(Session, "N", out _) ||
            !Guid.TryParseExact(Token, "N", out _) || !Guid.TryParseExact(Nonce, "N", out _))
            throw new InvalidDataException("Invalid snapshot context.");
        if (Kind == "request")
        {
            if (Snapshot != "" || Hash != "" || Index != 0 || Count != 0 || Data != "")
                throw new InvalidDataException("Snapshot request must not supply inventory data.");
        }
        else if (Kind == "chunk")
        {
            if (!Guid.TryParseExact(Snapshot, "N", out _) || Hash == null || Hash.Length != 64 ||
                Hash.Any(c => !(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'F')) ||
                Count < 1 || Count > MaxChunks || Index < 0 || Index >= Count || Data == null ||
                Data.Length < 1 || Data.Length > ChunkSize || (Index < Count - 1 && Data.Length != ChunkSize))
                throw new InvalidDataException("Invalid snapshot chunk.");
        }
        else throw new InvalidDataException("Unknown snapshot packet kind.");
    }
    internal static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static InventorySnapshotWire[] Split(InventorySnapshotWire request, string inventory)
    {
        request.Validate();
        if (request.Kind != "request") throw new InvalidDataException("Expected snapshot request.");
        ValidateInventory(inventory);
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(inventory));
        if (payload.Length > MaxPayload) throw new InvalidDataException("Encoded snapshot exceeds transfer limit.");
        string id = Guid.NewGuid().ToString("N"), hash = Digest(payload);
        int count = (payload.Length + ChunkSize - 1) / ChunkSize;
        return Enumerable.Range(0, count).Select(i => new InventorySnapshotWire
        {
            Build = request.Build, Kind = "chunk", Session = request.Session, Token = request.Token,
            Nonce = request.Nonce, Snapshot = id, Hash = hash, Index = i, Count = count,
            Data = payload.Substring(i * ChunkSize, Math.Min(ChunkSize, payload.Length - i * ChunkSize))
        }).ToArray();
    }
    internal static void ValidateInventory(string inventory)
    {
        if (string.IsNullOrEmpty(inventory) || inventory.Length > MaxPayload)
            throw new InvalidDataException("Snapshot inventory size exceeds the transfer limit.");
        using var json = JsonDocument.Parse(inventory);
        if (json.RootElement.ValueKind != JsonValueKind.Object ||
            !json.RootElement.TryGetProperty("Items", out var items) ||
            items.ValueKind != JsonValueKind.Array || items.GetArrayLength() != 9)
            throw new InvalidDataException("Expected eight native hotbar slots and the native wallet slot.");
        foreach (var item in items.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                throw new InvalidDataException("Invalid native item serialization.");
        string cleaned = inventory;
        string? backpack = BackpackSave.Extract(ref cleaned);
        if (backpack != null) _ = BackpackSave.Decode(backpack);
    }
}

internal sealed class InventorySnapshotReceiver
{
    private readonly InventorySnapshotWire request;
    private string? id, hash;
    private string?[]? chunks;
    private int received;
    private bool failed;
    internal string? Inventory { get; private set; }
    internal InventorySnapshotReceiver(InventorySnapshotWire request) => this.request = request;
    internal bool Accept(InventorySnapshotWire chunk)
    {
        if (failed) throw new InvalidOperationException("Snapshot receiver is faulted.");
        // Old connection/session traffic cannot poison the current receiver.
        if (!chunk.Matches(request)) return false;
        try
        {
            // Validate even when called directly instead of through Decode.
            _ = chunk.Encode();
            if (chunk.Kind != "chunk") throw new InvalidDataException("Expected snapshot chunk.");
            if (chunks == null)
            { id = chunk.Snapshot; hash = chunk.Hash; chunks = new string?[chunk.Count]; }
            if (id != chunk.Snapshot || hash != chunk.Hash || chunks.Length != chunk.Count)
                throw new InvalidDataException("Mixed snapshots rejected.");
            if (chunks[chunk.Index] != null)
            {
                if (chunks[chunk.Index] != chunk.Data) throw new InvalidDataException("Conflicting snapshot replay.");
                return Inventory != null;
            }
            chunks[chunk.Index] = chunk.Data; received++;
            if (received != chunks.Length) return false;
            string complete = string.Concat(chunks);
            if (InventorySnapshotWire.Digest(complete) != hash) throw new InvalidDataException("Snapshot integrity mismatch.");
            string inventory = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(complete));
            InventorySnapshotWire.ValidateInventory(inventory);
            Inventory = inventory;
            return true;
        }
        catch { failed = true; Inventory = null; chunks = null; throw; }
    }
}
