using MelonLoader;

[assembly: MelonInfo(typeof(EnhancedStorageBackpack.Mod), "Enhanced Storage + Backpack", "0.0.1", "codexgaming-de")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace EnhancedStorageBackpack;

public sealed class Mod : MelonMod
{
    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("ESB_BOOTSTRAP_OK | 0.0.1 | Grundprojekt geladen.");
    }
}
