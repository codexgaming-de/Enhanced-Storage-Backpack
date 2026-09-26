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
            for (int age = 0; age < 3; age++)
            {
                var text = File.ReadAllText(path + (age == 0 ? "" : "." + age));
                if (!text.Contains("session=" + (5 - age)) || !text.Contains("continued=" + (5 - age)) ||
                    text.Contains("disabled") || text.Contains("legacy"))
                    throw new Exception("Incorrect session retention");
            }
            if (Directory.GetFiles(directory).Length != 3) throw new Exception("Expected exactly three session logs");
            Console.WriteLine("PASS: five sessions retain exactly the last three; toggling does not rotate; legacy logs removed.");
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
