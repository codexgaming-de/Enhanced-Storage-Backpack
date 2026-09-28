using EnhancedStorageBackpack;
using System.Text.Json;
using System.Text.Json.Nodes;
using static EnhancedStorageBackpack.RemotePlayerJournal;
int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); }
    catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or JsonException or KeyNotFoundException)
    { rejected = true; }
    Check(rejected, name);
}
string empty = "{\"DataType\":\"ItemData\",\"ID\":\"\",\"Quantity\":0}";
string item = "{\"DataType\":\"ItemData\",\"ID\":\"test\",\"Quantity\":7}";
Snapshot Pair(string code, bool inBackpack)
{
    var slots = Enumerable.Repeat(empty, 9).ToArray();
    if (!inBackpack) slots[0] = item;
    string inventory = BackpackSave.Attach(JsonSerializer.Serialize(new { Items = slots, SlotFilters = new object?[9] }),
        BackpackSave.Encode(new string?[] { inBackpack ? item : null }));
    return new Snapshot(JsonSerializer.Serialize(new { PlayerCode = code }), inventory, "", "{}", "{}");
}
var journal = new RemotePlayerJournal();
var initial = Pair("123", false); var moved = Pair("123", true);
string directory = Path.Combine(Path.GetTempPath(), "esb-journal-test-" + Guid.NewGuid().ToString("N"));
try
{
    journal.Capture("123", 11, initial);
    Check(!Directory.Exists(directory), "capture never writes files");
    Check(journal.IsBound("123", 11) && !journal.Detached("123"), "native owner bound");
    journal.Capture("123", 11, moved);
    Check(journal.Read("123") == moved, "latest coupled pair held in RAM");
    Reject(() => journal.Capture("123", 22, initial), "new native object cannot overwrite live pair");
    journal.Detach("123", 999);
    Check(!journal.Detached("123"), "stale teardown cannot detach another binding");
    journal.Detach("123", 11);
    Check(journal.Read("123") == moved && !Directory.Exists(directory), "disconnect preserves live state without autosave");
    Reject(() => journal.Capture("123", 11, initial), "late capture cannot resurrect a detached player's old pair");
    var offline = journal.OfflineSnapshots(new HashSet<string>());
    Check(offline.Length == 1 && offline[0].Value == moved, "world save includes detached player's latest pair");
    Check(journal.OfflineSnapshots(new HashSet<string> { "123" }).Length == 0, "native connected save cannot race offline writer");
    Reject(() => journal.Rebind("123", 22, initial.Inventory), "rejoin cannot bind an unrestored native hotbar");
    journal.Rebind("123", 22, moved.Inventory);
    Check(journal.Read("123") == moved && journal.IsBound("123", 22), "reconnect retains unsaved inventory");
    Check(!Directory.Exists(directory), "reconnect does not persist unsaved move");
    Reject(() => journal.Rebind("123", 33, moved.Inventory), "concurrent second native owner rejected");
    Reject(() => journal.Rebind("123", 0, moved.Inventory), "null native owner rejected");
    journal.Detach("123", 11);
    Check(journal.IsBound("123", 22), "old disconnect ignored after rejoin");
    journal.Capture("123", 22, moved);
    // Explicit world save only. The native runtime owns the IsSaving gate.
    WriteOffline(directory, "123", initial);
    string path = Path.Combine(directory, "Player_123", "Inventory.json");
    Check(File.ReadAllText(path) == initial.Inventory, "initial save contains coupled pair");
    journal.Capture("123", 22, moved);
    journal.Detach("123", 22);
    Check(File.ReadAllText(path) == initial.Inventory, "unsaved move plus leave does not change disk");
    var restarted = new RemotePlayerJournal();
    restarted.Capture("123", 44, initial with { Inventory = File.ReadAllText(path) });
    Check(restarted.Read("123").Inventory == initial.Inventory, "host restart restores old hotbar AND old backpack");
    File.WriteAllText(Path.Combine(directory, "Player_123", "OtherMod.json"), "keep");
    foreach (var entry in journal.OfflineSnapshots(new HashSet<string>())) WriteOffline(directory, entry.Key, entry.Value);
    Check(File.ReadAllText(path) == moved.Inventory, "explicit world save advances hotbar and backpack together");
    string saved = File.ReadAllText(path);
    string? backpack = BackpackSave.Extract(ref saved);
    Check(BackpackSave.Decode(backpack!)[0] == item && JsonNode.Parse(saved)!["Items"]![0]!.GetValue<string>() == empty,
        "saved transfer has no duplicate in hotbar");
    Check(File.ReadAllText(Path.Combine(directory, "Player_123", "OtherMod.json")) == "keep", "other mod files untouched");
    Check(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories).Length == 0, "no staging files remain after save");
    var files = Directory.GetFiles(Path.Combine(directory, "Player_123"));
    Check(files.Length == 5, "only native save files plus existing foreign file");
    var withAppearance = moved with { Appearance = "{\"appearance\":true}" };
    WriteOffline(directory, "123", withAppearance);
    Check(File.ReadAllText(Path.Combine(directory, "Player_123", "Appearance.json")) == withAppearance.Appearance, "appearance preserved for new offline player");
    journal.Reset();
    Check(!journal.Contains("123"), "world switch clears session cache");
    Check(File.ReadAllText(path) == moved.Inventory, "reset never deletes saved inventory");
    Reject(() => journal.Read("123"), "no state leakage into next world");
    foreach (string code in new[] { "../123", "123/456", "0", " 123", "+123", "0123" })
        Reject(() => journal.Capture(code, 1, initial), "identity cannot escape player directory");
    Reject(() => WriteOffline(directory, "../evil", initial), "writer independently validates identity");
    Reject(() => journal.Capture("123", 0, initial), "null binding rejected");
    Reject(() => journal.Capture("123", 1, initial with { Player = "{\"PlayerCode\":\"456\"}" }), "cross-player data rejected");
    Reject(() => journal.Capture("123", 1, initial with { Inventory = "{}" }), "missing hotbar rejected");
    Reject(() => journal.Capture("123", 1, initial with { Inventory = "{" }), "malformed inventory rejected");
    Reject(() => journal.Capture("123", 1, initial with { Clothing = "null" }), "incomplete native subfile rejected");
    Check(!journal.Contains("123"), "failed captures never register success");
    journal.Capture("123", 1, initial);
    Reject(() => journal.Capture("123", 1, moved with { Variables = "{" }), "invalid replacement rejected");
    Check(journal.Read("123") == initial, "capture validates all subfiles before replacing state");
    journal.Fault("123");
    Reject(() => journal.Read("123"), "failed native capture cannot restore older pair");
    Reject(() => journal.OfflineSnapshots(new HashSet<string>()), "failed native capture blocks world flush");
    Reject(() => journal.Capture("123", 1, moved), "ordinary capture cannot hide fault");
    journal.Detach("123", 1);
    Reject(() => journal.Rebind("123", 2, initial.Inventory), "rejoin cannot hide fault");
    journal.Reset(); journal.Capture("123", 1, initial);
    Check(journal.Read("123") == initial, "new world can load trusted disk state after reset");
    for (int i = 1; i <= 63; i++) journal.Capture((1000 + i).ToString(), i, Pair((1000 + i).ToString(), false));
    Reject(() => journal.Capture("99999", 1, Pair("99999", false)), "session entry count bounded");
    journal.Reset();
    string large = JsonSerializer.Serialize(new { value = new string('x', 900000) });
    for (int i = 1; i <= 4; i++) journal.Capture(i.ToString(), i, Pair(i.ToString(), false) with { Clothing = large });
    Reject(() => journal.Capture("5", 5, Pair("5", false) with { Clothing = large }), "total retained snapshot budget bounded");
    journal.Capture("1", 1, Pair("1", false));
    journal.Capture("5", 5, Pair("5", false) with { Clothing = large });
    Check(journal.Contains("5"), "replacing a large snapshot releases its budget");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
Console.WriteLine($"Remote player journal: {checks} assertions passed; isolated filesystem exercised, native hooks not simulated.");
