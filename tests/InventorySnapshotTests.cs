using EnhancedStorageBackpack;
using System.Text.Json;
using System.Text.Json.Nodes;
int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or JsonException or InvalidOperationException or FormatException)
    { rejected = true; }
    Check(rejected, name);
}
InventorySnapshotWire Clone(InventorySnapshotWire value) => InventorySnapshotWire.Decode(value.Encode());
var probe = new InventoryChannelProbe { Session = Guid.NewGuid().ToString("N"),
    Token = Guid.NewGuid().ToString("N"), Nonce = Guid.NewGuid().ToString("N") };
var request = InventorySnapshotWire.Request(probe);
Check(Clone(request).Matches(request), "request roundtrip");
string empty = "{\"DataType\":\"ItemData\",\"ID\":\"\",\"Quantity\":0}";
string native = JsonSerializer.Serialize(new { Items = Enumerable.Repeat(empty, 9).ToArray(),
    SlotFilters = new object?[9], FutureField = "preserve" });
string backpack = BackpackSave.Encode(new string?[] { "{\"DataType\":\"ItemData\",\"ID\":\"test\",\"Quantity\":10}", null });
string inventory = BackpackSave.Attach(native, backpack);
var packets = InventorySnapshotWire.Split(request, inventory);
var receiver = new InventorySnapshotReceiver(request);
foreach (var packet in packets) receiver.Accept(Clone(packet));
Check(receiver.Inventory == inventory, "exact coupled native inventory and backpack");
Check(receiver.Accept(Clone(packets[0])), "identical replay harmless");
string loaded = receiver.Inventory!;
Check(BackpackSave.Extract(ref loaded) == backpack, "backpack survives transport");
Check(JsonNode.Parse(loaded)!["FutureField"]!.GetValue<string>() == "preserve", "unknown native fields survive");
Check(JsonNode.Parse(loaded)!["Items"]!.AsArray().Count == 9, "wallet slot retained");
// Raw non-ASCII, including surrogate pairs, must survive every chunk boundary.
var root = JsonNode.Parse(inventory)!.AsObject();
root["FutureField"] = new string('x', 7000);
string large = root.ToJsonString().Replace(new string('x', 7000), string.Concat(Enumerable.Repeat("ä🙂", 3000)));
var many = InventorySnapshotWire.Split(request, large);
Check(many.Length > 2, "multi-packet fixture");
receiver = new InventorySnapshotReceiver(request);
foreach (var packet in many.Reverse().SkipLast(1)) Check(!receiver.Accept(Clone(packet)), "partial snapshot never visible");
Check(receiver.Inventory == null, "no partial state published");
Check(receiver.Accept(Clone(many[0])) && receiver.Inventory == large, "out-of-order unicode snapshot restored");
receiver = new InventorySnapshotReceiver(request);
var other = Clone(many[0]); other.Token = Guid.NewGuid().ToString("N");
Check(!receiver.Accept(other), "old connection ignored");
other.Token = request.Token; other.Session = Guid.NewGuid().ToString("N");
Check(!receiver.Accept(other), "other game ignored");
other.Session = request.Session; other.Nonce = Guid.NewGuid().ToString("N");
Check(!receiver.Accept(other), "old request ignored");
foreach (var p in many) receiver.Accept(p);
Check(receiver.Inventory == large, "unrelated traffic did not poison receiver");
receiver = new InventorySnapshotReceiver(request); receiver.Accept(many[0]);
var conflict = Clone(many[0]); conflict.Data = "A" + conflict.Data[1..];
Reject(() => receiver.Accept(conflict), "conflicting replay faults receiver");
Check(receiver.Inventory == null, "fault clears any published inventory");
Reject(() => receiver.Accept(many[1]), "faulted receiver never resumes");
receiver = new InventorySnapshotReceiver(request); receiver.Accept(many[0]);
var mixed = Clone(many[1]); mixed.Snapshot = Guid.NewGuid().ToString("N");
Reject(() => receiver.Accept(mixed), "two snapshots cannot be mixed");
var corrupt = packets.Select(Clone).ToArray(); corrupt[^1].Data = "A" + corrupt[^1].Data[1..];
receiver = new InventorySnapshotReceiver(request);
Reject(() => { foreach (var p in corrupt) receiver.Accept(p); }, "integrity failure rejects entire inventory");
var forged = Clone(request); forged.Data = inventory;
Reject(() => forged.Encode(), "client cannot supply items in snapshot request");
forged = Clone(request); forged.Count = 1;
Reject(() => forged.Encode(), "client cannot allocate receiving buffers on host");
foreach (int count in new[] { -1, 0, 65, int.MaxValue })
{
    var bad = Clone(many[0]); bad.Count = count;
    Reject(() => bad.Encode(), "chunk count bounded");
}
foreach (int index in new[] { -1, many.Length, int.MaxValue })
{
    var bad = Clone(many[0]); bad.Index = index;
    Reject(() => bad.Encode(), "chunk index bounded");
}
var shortChunk = Clone(many[0]); shortChunk.Data = "a";
Reject(() => shortChunk.Encode(), "nonfinal chunk size fixed");
Reject(() => InventorySnapshotWire.Decode(new string('x', 27000)), "packet size bounded before parsing");
Reject(() => InventorySnapshotWire.Decode("null"), "null rejected");
Reject(() => InventorySnapshotWire.Decode("{}"), "missing context rejected");
Reject(() => InventorySnapshotWire.Decode(request.Encode().Replace(MultiplayerProtocol.Build, "old")), "old build rejected");
Reject(() => InventorySnapshotWire.Split(request, "{}"), "missing hotbar rejected");
Reject(() => InventorySnapshotWire.Split(request, native.Replace("\"Items\"", "\"Other\"")), "missing items rejected");
Reject(() => InventorySnapshotWire.Split(request, JsonSerializer.Serialize(new { Items = new string[8] })), "wallet omission rejected");
Reject(() => InventorySnapshotWire.Split(request, JsonSerializer.Serialize(new { Items = new string[9] })), "null native items rejected");
Reject(() => InventorySnapshotWire.Split(request, BackpackSave.Attach(native, "{\"Version\":2,\"Items\":[]}")), "unknown backpack version rejected");
Reject(() => InventorySnapshotWire.Split(request, BackpackSave.Attach(native, BackpackSave.Encode(new string?[129]))), "backpack capacity bounded");
root["FutureField"] = new string('x', InventorySnapshotWire.MaxPayload);
Reject(() => InventorySnapshotWire.Split(request, root.ToJsonString()), "payload allocation bounded");
root["FutureField"] = new string('x', 200000);
Reject(() => InventorySnapshotWire.Split(request, root.ToJsonString()), "encoded payload also bounded");
var vanilla = InventorySnapshotWire.Split(request, native);
receiver = new InventorySnapshotReceiver(request); foreach (var p in vanilla) receiver.Accept(p);
Check(receiver.Inventory == native, "vanilla backpack absence preserved");
Console.WriteLine($"Inventory snapshot transport: {checks} assertions passed; native RPC hooks not simulated.");
