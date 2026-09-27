using EnhancedStorageBackpack;

internal static class DiagnosticLogTests
{
    public static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "esb-log-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        MelonLoader.Utils.MelonEnvironment.UserDataDirectory = directory;
        var path = Path.Combine(directory, "Enhanced-Storage-Backpack-Debug.log");
        try
        {
            File.WriteAllText(path, "legacy mixed sessions");
            File.WriteAllText(path + ".previous", "legacy rotation");
            for (int session = 1; session <= 5; session++)
            {
                var log = new DiagnosticLog(new MelonLoader.MelonLogger.Instance());
                log.Write(false, "disabled");
                log.Write(true, "session=" + session);
                log.Write(false, "disabled");
                log.Write(true, "continued=" + session);
            }
            var text = File.ReadAllText(path);
            for (int session = 3; session <= 5; session++)
                if (!text.Contains("session=" + session) || !text.Contains("continued=" + session))
                    throw new Exception("Missing retained session");
            if (text.Contains("session=1") || text.Contains("session=2") || text.Contains("disabled") || text.Contains("legacy"))
                throw new Exception("Unexpected old/disabled data");
            if (text.Split(" | ESB_SESSION_START").Length - 1 != 3 || Directory.GetFiles(directory).Length != 1)
                throw new Exception("Expected three sessions in exactly one file");

            // Simulate upgrading the previous three-file format.
            File.WriteAllText(path + ".2", "old | ESB_SESSION_START\narchive-oldest\n");
            File.WriteAllText(path + ".1", "old | ESB_SESSION_START\narchive-previous\n");
            File.WriteAllText(path, "old | ESB_SESSION_START\narchive-current\n");
            new DiagnosticLog(new MelonLoader.MelonLogger.Instance()).Write(true, "migration-new");
            text = File.ReadAllText(path);
            if (!text.Contains("archive-previous") || !text.Contains("archive-current") || !text.Contains("migration-new") ||
                text.Contains("archive-oldest") || Directory.GetFiles(directory).Length != 1)
                throw new Exception("Migration failed");
            Console.WriteLine("PASS: single file retains last three sessions; toggles do not rotate; numbered archives migrate correctly.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}

namespace MelonLoader
{
    internal static class MelonLogger
    {
        internal sealed class Instance
        {
            public void Warning(string message) => throw new Exception(message);
        }
    }
}
namespace MelonLoader.Utils
{
    internal static class MelonEnvironment
    {
        public static string UserDataDirectory { get; set; } = "";
    }
}
