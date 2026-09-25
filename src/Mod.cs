using MelonLoader;

[assembly: MelonInfo(typeof(EnhancedStorageBackpack.Mod), "Enhanced Storage + Backpack", "0.0.3", "codexgaming-de")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace EnhancedStorageBackpack;

public sealed class Mod : MelonMod
{
    private Settings? settings;

    public override void OnInitializeMelon()
    {
        settings = new Settings(LoggerInstance);
        LoggerInstance.Msg("ESB_SETTINGS_READY | 0.0.3 | 22 Einstellungen registriert; Funktionstest der Einstellungen.");
    }

    public override void OnDeinitializeMelon()
    {
        settings?.Dispose();
        settings = null;
    }
}
