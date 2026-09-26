using HarmonyLib;
using Il2CppFishNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.UI;
using MelonLoader;

[assembly: MelonInfo(typeof(EnhancedStorageBackpack.Mod), "Enhanced Storage + Backpack", "0.0.5", "codexgaming-de")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace EnhancedStorageBackpack;

public sealed class Mod : MelonMod
{
    private static Mod? instance;
    private Settings? settings;
    private SmallRackStorage? storage;
    private bool disabled;
    private bool refreshSettingsUi;

    public override void OnInitializeMelon()
    {
        instance = this;
        settings = new Settings(LoggerInstance);
        settings.LanguageChanged += RequestSettingsRefresh;
        storage = new SmallRackStorage(settings);
        try
        {
            Patch(typeof(StorageEntity), "Start", postfix: nameof(StorageStarted));
            Patch(typeof(PlaceableStorageEntity), "InitializeGridItem", postfix: nameof(Placed));
            Patch(typeof(StorageEntity), "OnDestroy", prefix: nameof(Destroyed));
            Patch(typeof(StorageEntity), "ContentsChanged", postfix: nameof(ContentsChanged));
            Patch(typeof(StorageEntity), "LoadFromItemSet", prefix: nameof(Loading));
            var open = AccessTools.Method(typeof(StorageMenu), "Open", new[] { typeof(StorageEntity), typeof(Il2CppSystem.Action) });
            HarmonyInstance.Patch(open, new HarmonyMethod(typeof(Mod), nameof(Opening)), new HarmonyMethod(typeof(Mod), nameof(Opened)));
            Patch(typeof(StorageMenu), "OnClose", postfix: nameof(Closed));
            LoggerInstance.Msg(settings.Text("ESB_READY | 0.0.5 | Kleines Lagerregal aktiviert.", "ESB_READY | 0.0.5 | Small storage rack enabled."));
            settings.Trace("ESB_READY | 0.0.5");
        }
        catch (Exception ex)
        {
            disabled = true;
            HarmonyInstance.UnpatchSelf();
            settings.Error("ESB_INITIALIZATION", ex);
        }
    }

    private void Patch(Type type, string method, string? prefix = null, string? postfix = null)
        => HarmonyInstance.Patch(AccessTools.Method(type, method),
            prefix == null ? null : new HarmonyMethod(typeof(Mod), prefix),
            postfix == null ? null : new HarmonyMethod(typeof(Mod), postfix));

    private static bool Active => instance != null && !instance.disabled && !InstanceFinder.IsClientOnly;

    private static void Run(Action<SmallRackStorage> action)
    {
        if (!Active) return;
        try { action(instance!.storage!); }
        catch (Exception ex)
        {
            instance!.disabled = true;
            instance.settings!.Error("ESB_STORAGE_DISABLED", ex);
        }
    }

    public override void OnUpdate()
    {
        RefreshSettingsUi();
        if (!Active) return;
        try { storage!.Tick(); }
        catch (Exception ex) { disabled = true; settings!.Error("ESB_UPDATE_DISABLED", ex); }
    }

    private void RequestSettingsRefresh() => refreshSettingsUi = true;

    private void RefreshSettingsUi()
    {
        if (!refreshSettingsUi) return;
        refreshSettingsUi = false;
        // Defer until after the dropdown callback; rebuilding it inside its own
        // value-change callback would destroy controls still handling that event.
        try
        {
            var manager = MelonBase.RegisteredMelons.FirstOrDefault(m =>
                m.GetType().FullName == "ModManagerPhoneApp.ModSettingsAppCreator");
            if (manager == null) return;
            var refresh = manager.GetType().GetMethod("TriggerUIRefresh", Type.EmptyTypes);
            if (refresh == null)
            {
                settings?.Trace("ESB_MANAGER_REFRESH | API unavailable");
                return;
            }
            // Record the actual API gates: a successful call can still be a no-op.
            var managerType = manager.GetType();
            object? Read(string name) => managerType.GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic)?.GetValue(manager);
            string Panel(string name)
            {
                var panel = Read(name) as UnityEngine.GameObject;
                return panel == null ? "missing" : $"active={panel.activeInHierarchy}";
            }
            var selected = Read("_currentlySelectedMelon") as MelonBase;
            settings?.Trace($"ESB_MANAGER_STATE | menuScene={Read("_isInMenuScene")} | gameScene={Read("_isInGameScene")} | modsTab={Read("_isMenuModsTabActive")} | selected={selected?.Info?.Name ?? "all/null"} | menu={Panel("_mainMenuModPanelInstance")} | phone={Panel("ModManagerPanel")} | app={Panel("modManagerAppInstance")}");
            refresh.Invoke(manager, null);
            settings?.Trace("ESB_MANAGER_REFRESH | requested");
        }
        catch (Exception ex) { settings?.Error("ESB_MANAGER_REFRESH", ex); }
    }

    private static void StorageStarted(StorageEntity __instance) => Run(s => s.Started(__instance));
    private static void Placed(PlaceableStorageEntity __instance) => Run(s => s.Started(__instance.StorageEntity));
    private static void Destroyed(StorageEntity __instance) => Run(s => s.Removed(__instance));
    private static void ContentsChanged(StorageEntity __instance) => Run(s => s.ContentsChanged(__instance));
    private static void Opening(StorageEntity __0) => Run(s => s.BeforeOpen(__0));
    private static void Opened(StorageEntity __0) => Run(s => s.AfterOpen(__0));
    private static void Closed() => Run(s => s.Closed());
    private static void Loading(StorageEntity __instance, Il2CppReferenceArray<ItemInstance> __0)
    {
        if (!Active) return;
        try { instance!.storage!.BeforeLoad(__instance, __0); }
        catch (Exception ex)
        {
            instance!.settings!.Error("ESB_STORAGE_LOAD", ex);
            // Abort this call rather than handing the loader an undersized slot list.
            throw;
        }
    }

    public override void OnDeinitializeMelon()
    {
        disabled = true;
        HarmonyInstance.UnpatchSelf();
        storage?.Dispose();
        if (settings != null) settings.LanguageChanged -= RequestSettingsRefresh;
        settings?.Dispose();
        instance = null;
    }
}
