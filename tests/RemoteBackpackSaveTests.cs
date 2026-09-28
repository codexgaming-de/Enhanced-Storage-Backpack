using System.Text.Json.Nodes;
using EnhancedStorageBackpack;
int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (InvalidDataException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, name);
}
var cache = new RemoteBackpackSaves();
string old = "{\"Items\":[\"old hotbar\"],\"SlotFilters\":[null]}";
string current = "{\"Items\":[\"current hotbar\"],\"SlotFilters\":[{\"unknown\":true}],\"FutureField\":17}";
string backpack = BackpackSave.Encode(new string?[] { "{\"DataType\":\"ItemData\",\"ID\":\"test\",\"Quantity\":5}", null });
Reject(() => cache.Preserve("a", current), "unknown owner not silently saved");
cache.Load("a", BackpackSave.Attach(old, backpack));
string result = cache.Preserve("a", current);
string? restored = BackpackSave.Extract(ref result);
Check(restored == backpack, "host backpack preserved");
Check(JsonNode.Parse(result)!["Items"]![0]!.GetValue<string>() == "current hotbar", "old hotbar not restored");
Check(JsonNode.Parse(result)!["FutureField"]!.GetValue<int>() == 17, "unknown native field preserved");
Check(JsonNode.Parse(result)!["SlotFilters"]![0]!["unknown"]!.GetValue<bool>(), "native filter preserved");
cache.Load("a", BackpackSave.Attach(old, backpack));
Check(cache.Contains("a"), "idempotent baseline load");
string again = cache.Preserve("a", cache.Preserve("a", current));
Check(BackpackSave.Extract(ref again) == backpack, "getter then writer does not duplicate extension");
cache.Load("b", old);
Check(cache.Preserve("b", current) == current, "vanilla inventory untouched");
cache.Load("new", "{}");
Check(cache.Preserve("new", current) == current, "new player gets no other player's backpack");
Reject(() => cache.Load("a", old), "reload cannot erase baseline");
Reject(() => cache.Preserve("a", BackpackSave.Attach(current, BackpackSave.Encode(new string?[1]))), "conflicting backpack rejected");
Reject(() => cache.Load("bad", "{\"EnhancedStorageBackpack\":null}"), "null backpack rejected");
Reject(() => cache.Load("bad", "{\"EnhancedStorageBackpack\":{\"Version\":9,\"Items\":[]}}"), "unknown version rejected");
Check(!cache.Contains("bad"), "failed load never caches success");
cache.Reset();
Check(!cache.Contains("a"), "save slot switch clears baseline");
cache.Load("a", old);
Check(cache.Preserve("a", current) == current, "backpack cannot leak to another save slot");
for (int n = 0; n < 63; n++) cache.Load("peer" + n, "{}");
Reject(() => cache.Load("overflow", "{}"), "bounded cache");
Console.WriteLine($"Remote backpack save: {checks} assertions passed.");
