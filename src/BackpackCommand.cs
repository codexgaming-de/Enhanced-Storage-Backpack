using System.Text.Json;

namespace EnhancedStorageBackpack;

// Commands contain intent only. Item data travels exclusively from the host.
internal sealed class BackpackCommand
{
    public string Build { get; set; } = MultiplayerProtocol.Build;
    public string Session { get; set; } = "";
    public string Token { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Lease { get; set; } = "";
    public long Revision { get; set; }
    public bool FromBackpack { get; set; }
    public bool ToBackpack { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public int Amount { get; set; }
    public float CashAmount { get; set; }
    internal string Encode() { Validate(); return JsonSerializer.Serialize(this); }
    internal static BackpackCommand Decode(string text)
    {
        if (text == null || text.Length > 4096) throw new InvalidDataException("Command too large.");
        var command = JsonSerializer.Deserialize<BackpackCommand>(text) ?? throw new InvalidDataException("Missing command.");
        command.Validate(); return command;
    }
    private void Validate()
    {
        if (Build != MultiplayerProtocol.Build || !Guid.TryParseExact(Session, "N", out _) ||
            !Guid.TryParseExact(Token, "N", out _) || !Guid.TryParseExact(Nonce, "N", out _) ||
            Kind is not ("open" or "move" or "commit" or "abort" or "freeze" or "frozen" or "release" or "layout" or "recovered" or "resync"))
            throw new InvalidDataException("Invalid command context.");
        if (Kind == "move" && (!Guid.TryParseExact(Lease, "N", out _) || Revision < 0 ||
            From < 0 || From >= (FromBackpack ? 128 : 9) || To < 0 || To >= (ToBackpack ? 128 : 9) ||
            (!FromBackpack && !ToBackpack) || (FromBackpack == ToBackpack && From == To) || Amount < 1 || Amount > 1000000 || !float.IsFinite(CashAmount) || CashAmount < 0 || CashAmount > 1000))
            throw new InvalidDataException("Invalid move intent.");
    }
}

// A plan never commits until every changed native hotbar slot is acknowledged.
// Exact duplicate acknowledgements are harmless; unexpected values cancel it.
internal sealed class NativeMoveReceipts
{
    private readonly Dictionary<int, string?> expected;
    private readonly HashSet<int> received = new();
    internal NativeMoveReceipts(string?[] before, string?[] after)
    {
        if (before.Length != 9 || after.Length != 9) throw new ArgumentException("Expected native inventory.");
        expected = Enumerable.Range(0, 9).Where(i => before[i] != after[i]).ToDictionary(i => i, i => after[i]);
    }
    internal bool Contains(int index) => expected.ContainsKey(index);
    internal bool Complete => received.Count == expected.Count;
    internal bool Accept(int index, string? value)
    {
        if (!expected.TryGetValue(index, out var wanted) || wanted != value) return false;
        received.Add(index); return true;
    }
    internal int[] Indices => expected.Keys.OrderBy(i => i).ToArray();
}
