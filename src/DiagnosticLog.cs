using MelonLoader;
using MelonLoader.Utils;

namespace EnhancedStorageBackpack;

internal sealed class DiagnosticLog
{
    private const long MaxBytes = 1024 * 1024;
    private readonly MelonLogger.Instance logger;
    private bool unavailable;

    public DiagnosticLog(MelonLogger.Instance logger) => this.logger = logger;

    public void Write(bool enabled, string message)
    {
        if (!enabled || unavailable) return;
        try
        {
            var directory = MelonEnvironment.UserDataDirectory;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "Enhanced-Storage-Backpack-Debug.log");
            if (File.Exists(path) && new FileInfo(path).Length >= MaxBytes)
                File.Move(path, path + ".previous", overwrite: true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} | {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unavailable = true;
            logger.Warning($"Diagnose-Log konnte nicht geschrieben werden: {ex.Message}");
        }
    }
}
