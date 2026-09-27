using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EnhancedStorageBackpack;

internal static class BackpackSaveTests
{
    private static int checks;
    private static void Check(bool value) { checks++; if (!value) throw new Exception($"Check {checks} failed."); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception e) when (e is InvalidDataException || e is System.Text.Json.JsonException) { checks++; return; }
        throw new Exception("Invalid data accepted.");
    }
    public static void Main()
    {
        const string vanilla = "{\"Items\":[\"hotbar-item\",null],\"Other\":{\"value\":42}}";
        string copy = vanilla;
        Check(BackpackSave.Extract(ref copy) == null && copy == vanilla);
        for (int count = 1; count <= 128; count++)
        {
            var items = new string?[count];
            items[count - 1] = "{\"DataType\":\"ItemData\",\"ID\":\"test\",\"Quantity\":17}";
            string encoded = BackpackSave.Encode(items);
            string inventory = BackpackSave.Attach(vanilla, encoded);
            string? payload = BackpackSave.Extract(ref inventory);
            Check(payload != null && BackpackSave.Decode(payload).SequenceEqual(items));
            Check(JsonNode.Parse(inventory)!.ToJsonString() == JsonNode.Parse(vanilla)!.ToJsonString());
        }
        // A saved snapshot remains unchanged when in-memory state changes.
        string diskSnapshot = BackpackSave.Attach(vanilla, BackpackSave.Encode(new string?[1]));
        string changedSnapshot = BackpackSave.Attach("{\"Items\":[]}", BackpackSave.Encode(new[] { "transferred-item" }));
        copy = diskSnapshot;
        Check(BackpackSave.Decode(BackpackSave.Extract(ref copy)!)[0] == null);
        Check(JsonNode.Parse(copy)!["Items"]!.AsArray().Count == 2);
        copy = changedSnapshot;
        Check(BackpackSave.Decode(BackpackSave.Extract(ref copy)!)[0] == "transferred-item");
        Check(JsonNode.Parse(copy)!["Items"]!.AsArray().Count == 0);
        // Unknown payload versions can be preserved verbatim if runtime loading fails.
        string future = "{\"Version\":999,\"Items\":[\"unknown\"]}";
        copy = BackpackSave.Attach(vanilla, future);
        string extracted = BackpackSave.Extract(ref copy)!;
        Reject(() => BackpackSave.Decode(extracted));
        Check(BackpackSave.Attach(copy, extracted).Contains("999"));
        Reject(() => BackpackSave.Decode("{}"));
        Reject(() => BackpackSave.Decode("{\"Version\":1,\"Items\":null}"));
        Reject(() => BackpackSave.Decode(BackpackSave.Encode(new string?[129])));
        Reject(() => BackpackSave.Attach("[]", "{}"));
        Reject(() => BackpackSave.Decode("broken"));
        Console.WriteLine($"{checks} backpack persistence checks passed.");
    }
}
