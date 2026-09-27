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
    private long sessionBytes;

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
                var sessions = new Queue<System.Text.StringBuilder>();
                // Old numbered files are read oldest first for one-time migration.
                foreach (var source in new[] { path + ".2", path + ".1", path })
                {
                    if (!File.Exists(source)) continue;
                    System.Text.StringBuilder? session = null;
                    foreach (var line in File.ReadLines(source))
                    {
                        if (line.EndsWith(" | " + SessionMarker, StringComparison.Ordinal))
                        {
                            session = new System.Text.StringBuilder();
                            sessions.Enqueue(session);
                            while (sessions.Count > 2) sessions.Dequeue();
                        }
                        session?.AppendLine(line);
                    }
                }
                var history = new System.Text.StringBuilder();
                foreach (var session in sessions) history.Append(session);
                string header = $"{DateTimeOffset.Now:O} | {SessionMarker}{Environment.NewLine}";
                history.Append(header);
                // Replace only after writing the complete retained history.
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, history.ToString());
                File.Move(temporary, path, overwrite: true);
                foreach (var suffix in new[] { ".1", ".2", ".previous" }) File.Delete(path + suffix);
                sessionBytes = System.Text.Encoding.UTF8.GetByteCount(header);
                started = true;
            }
            if (sessionBytes >= MaxBytes)
            {
                File.AppendAllText(path!, $"{DateTimeOffset.Now:O} | ESB_LOG_LIMIT | Session log limit reached.{Environment.NewLine}");
                limitReached = true;
                return;
            }
            string record = $"{DateTimeOffset.Now:O} | {message}{Environment.NewLine}";
            File.AppendAllText(path!, record);
            sessionBytes += System.Text.Encoding.UTF8.GetByteCount(record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unavailable = true;
            logger.Warning($"Diagnose-Log konnte nicht geschrieben werden: {ex.Message}");
        }
    }
}
