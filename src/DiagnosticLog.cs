using MelonLoader;
using MelonLoader.Utils;

namespace EnhancedStorageBackpack;

internal sealed class DiagnosticLog
{
    private const long MaxBytes = 1024 * 1024;
    private const string SessionMarker = "ESB_SESSION_START";
    private readonly MelonLogger.Instance logger;
    private bool unavailable;
    private bool started;
    private bool limitReached;
    private string? path;

    public DiagnosticLog(MelonLogger.Instance logger) => this.logger = logger;

    public void Write(bool enabled, string message)
    {
        if (!enabled || unavailable || limitReached) return;
        try
        {
            if (!started)
            {
                var directory = MelonEnvironment.UserDataDirectory;
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory, "Enhanced-Storage-Backpack-Debug.log");
                // The legacy log mixed multiple sessions. Start a clean history
                // on upgrade rather than retaining an unknown number of sessions.
                if (File.Exists(path))
                {
                    string? firstLine;
                    using (var reader = File.OpenText(path)) firstLine = reader.ReadLine();
                    if (firstLine?.Contains(SessionMarker, StringComparison.Ordinal) != true)
                        File.Delete(path);
                }
                File.Delete(path + ".previous");
                File.Delete(path + ".2");
                if (File.Exists(path + ".1")) File.Move(path + ".1", path + ".2");
                if (File.Exists(path)) File.Move(path, path + ".1");
                File.WriteAllText(path, $"{DateTimeOffset.Now:O} | {SessionMarker}{Environment.NewLine}");
                started = true;
            }
            if (new FileInfo(path!).Length >= MaxBytes)
            {
                File.AppendAllText(path!, $"{DateTimeOffset.Now:O} | ESB_LOG_LIMIT | Session log limit reached.{Environment.NewLine}");
                limitReached = true;
                return;
            }
            File.AppendAllText(path!, $"{DateTimeOffset.Now:O} | {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unavailable = true;
            logger.Warning($"Diagnose-Log konnte nicht geschrieben werden: {ex.Message}");
        }
    }
}
