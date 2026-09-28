using System.Text.Json.Nodes;
using EnhancedStorageBackpack;

int checks = 0;
void Check(bool valid, string name)
{
    checks++;
    if (!valid) throw new Exception(name);
}
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
    Check(rejected, name);
}
MultiplayerProtocol.Offer Offer() => new()
{
    Protocol = MultiplayerProtocol.Version, Build = MultiplayerProtocol.Build,
    Session = Guid.NewGuid().ToString("N"), BackpackSlots = 40,
    Slots = new int[9], Rows = new int[9]
};
string original = "{\"Items\":[{\"ID\":\"sample\",\"Quantity\":3}],\"UnknownVanillaField\":true}";
string backpack = BackpackSave.Encode(new string?[] { "{\"ID\":\"sample\",\"Quantity\":2}", null });
string withBackpack = BackpackSave.Attach(original, backpack);
for (int capacity = 1; capacity <= 128; capacity++)
{
    var offer = Offer(); offer.BackpackSlots = capacity;
    offer.Slots = Enumerable.Repeat(capacity, 9).ToArray();
    offer.Rows = Enumerable.Repeat(capacity, 9).ToArray();
    string transmitted = MultiplayerProtocol.Attach(withBackpack, offer);
    // Entry point + Target/Observers writer detours may each attach metadata.
    transmitted = MultiplayerProtocol.Attach(transmitted, offer);
    var decoded = MultiplayerProtocol.Extract(ref transmitted)!;
    Check(decoded.Session == offer.Session && decoded.BackpackSlots == capacity, "round trip");
    Check(decoded.Slots.SequenceEqual(offer.Slots) && decoded.Rows.SequenceEqual(offer.Rows), "host settings");
    Check(JsonNode.Parse(transmitted)!.ToJsonString() == JsonNode.Parse(withBackpack)!.ToJsonString(), "preserve payload");
    Check(BackpackSave.Extract(ref transmitted) == backpack, "preserve backpack");
    Check(JsonNode.Parse(transmitted)!.ToJsonString() == JsonNode.Parse(original)!.ToJsonString(), "preserve hotbar");
    Check(MultiplayerProtocol.Extract(ref transmitted) == null, "duplicate receive harmless");
}
foreach (int invalid in new[] { -1, 0, 129, int.MaxValue, int.MinValue })
{
    var o = Offer(); o.BackpackSlots = invalid;
    Reject(() => MultiplayerProtocol.Attach(original, o), "invalid backpack size");
}
foreach (int invalid in new[] { -1, 129, int.MaxValue, int.MinValue })
for (int slot = 0; slot < 9; slot++)
{
    var o = Offer(); o.Slots[slot] = invalid;
    Reject(() => MultiplayerProtocol.Attach(original, o), "invalid rack size");
    o = Offer(); o.Rows[slot] = invalid;
    Reject(() => MultiplayerProtocol.Attach(original, o), "invalid row count");
}
foreach (var mutate in new Action<MultiplayerProtocol.Offer>[]
{
    o => o.Protocol = 2, o => o.Build = "0.1.6", o => o.Session = "",
    o => o.Slots = new int[8], o => o.Rows = new int[10],
    o => o.Slots = null!, o => o.Rows = null!
})
{
    var o = Offer(); mutate(o);
    Reject(() => MultiplayerProtocol.Attach(original, o), "bad offer");
}
foreach (string bad in new[] { "[]", "null", "{", "", new string('x', MultiplayerProtocol.MaximumInventoryCharacters + 1) })
    Reject(() => MultiplayerProtocol.Attach(bad, Offer()), "bad envelope");
var incompatible = JsonNode.Parse(MultiplayerProtocol.Attach(withBackpack, Offer()))!;
incompatible[MultiplayerProtocol.Key]!["Build"] = "other-version";
string received = incompatible.ToJsonString();
Reject(() => MultiplayerProtocol.Extract(ref received), "reject incompatible host");
Check(!JsonNode.Parse(received)!.AsObject().ContainsKey(MultiplayerProtocol.Key), "strip rejected metadata");
Check(JsonNode.Parse(received)!.ToJsonString() == JsonNode.Parse(withBackpack)!.ToJsonString(), "rejection preserves inventory");
string vanilla = original;
Check(MultiplayerProtocol.Extract(ref vanilla) == null && vanilla == original, "vanilla host unchanged");
var zeroDefaults = Offer();
string zeroWire = MultiplayerProtocol.Attach(original, zeroDefaults);
Check(MultiplayerProtocol.Extract(ref zeroWire)!.Slots.All(x => x == 0), "zero means vanilla");
Console.WriteLine($"Multiplayer protocol: {checks} checks passed (no native networking simulated).");
