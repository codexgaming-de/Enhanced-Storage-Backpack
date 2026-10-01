// Enhanced Storage + Backpack
// Author / Projektleitung: CoDeX-Gaming
// Developed with OpenAI Codex assistance; see README.md for disclosure.

using HarmonyLib;
using Il2CppFishNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.UI;
using MelonLoader;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Persistence;

[assembly: MelonInfo(typeof(EnhancedStorageBackpack.Mod), "Enhanced Storage + Backpack", EnhancedStorageBackpack.MultiplayerProtocol.Build, "CoDeX-Gaming")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace EnhancedStorageBackpack;

public sealed class Mod : MelonMod
{
    private static Mod? instance;
    private Settings? settings;
    private RackStorage? storage;
    private Backpack? backpack;
    private MultiplayerDiagnostics? multiplayer;
    private SteamHostSettings? steamSettings;
    private NativeInventoryChannel? nativeChannel;
    private NativeRemotePlayerJournal? remoteJournal;
    private RemoteBackpackRuntime? remoteBackpack;
    private readonly RemoteBackpackSaves remoteSaves = new();
    private bool disabled;
    private bool refreshSettingsUi;

    public override void OnInitializeMelon()
    {
        instance = this;
        settings = new Settings(LoggerInstance);
        settings.LanguageChanged += RequestSettingsRefresh;
        storage = new RackStorage(settings);
        try
        {
            backpack = new Backpack(settings);
            multiplayer = new MultiplayerDiagnostics(settings);
            steamSettings = new SteamHostSettings(settings, multiplayer.HostOffer);
            remoteJournal = new NativeRemotePlayerJournal(settings, p => PreserveRemoteInventory(p, NativeRemotePlayerJournal.ReadNativeInventory(p)), HarmonyInstance);
            nativeChannel = new NativeInventoryChannel(settings, steamSettings, HarmonyInstance, ReadRemoteSnapshot);
            remoteBackpack = new RemoteBackpackRuntime(settings, nativeChannel, storage, HarmonyInstance, ReadRemoteSnapshot, (p, items) =>
            {
                remoteSaves.UpdateLive(p.PlayerCode, items);
                remoteJournal.Admit(p, PreserveRemoteInventory(p, NativeRemotePlayerJournal.ReadNativeInventory(p)));
            });
            Patch(typeof(Player), "OnStartClient", postfix: nameof(NetworkPlayerStarted));
            Patch(typeof(Player), "RequestSavePlayer", prefix: nameof(NetworkSaveRequested));
            Patch(typeof(Player), "RpcLogic___RequestSavePlayer_2166136261", prefix: nameof(NetworkSaveReceived));
            Patch(typeof(Player), "RpcLogic___ReceivePlayerData_3244732873", prefix: nameof(NetworkPlayerReceiving));
            Patch(typeof(Player), "GetInventoryString", postfix: nameof(InventorySaving));
            Patch(typeof(ISaveable), "WriteSubfile", prefix: nameof(InventorySubfileWriting));
            Patch(typeof(Player), "LoadInventory", prefix: nameof(InventoryLoading));
            // Narrow save-path diagnostics: observe boundaries without writing
            // files, changing native return values or forcing extra saves.
            Patch(typeof(Player), "WriteData", prefix: nameof(PlayerWriteStarting), postfix: nameof(PlayerWriteFinished));
            Patch(typeof(PlayerManager), "WriteData", prefix: nameof(ManagerWriteStarting), postfix: nameof(ManagerWriteFinished));
            Patch(typeof(PlayerManager), "SavePlayer", prefix: nameof(ManagerPlayerSaving));
            foreach (var save in typeof(SaveManager).GetMethods().Where(m => m.Name == "Save" &&
                (m.GetParameters().Length == 0 || (m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string)))))
                HarmonyInstance.Patch(save, prefix: new HarmonyMethod(typeof(Mod), nameof(SaveRequested)));
            Patch(typeof(LoadManager), "StartGame", prefix: nameof(GameStarting));
            Patch(typeof(StorageEntity), "Start", postfix: nameof(StorageStarted));
            Patch(typeof(StorageEntityVisualizer), "Start", postfix: nameof(VisualizerStarted));
            Patch(typeof(PlaceableStorageEntity), "InitializeGridItem", postfix: nameof(Placed));
            Patch(typeof(StorageEntity), "OnDestroy", prefix: nameof(Destroyed));
            Patch(typeof(StorageEntity), "ContentsChanged", postfix: nameof(ContentsChanged));
            Patch(typeof(StorageEntity), "LoadFromItemSet", prefix: nameof(Loading));
            var open = AccessTools.Method(typeof(StorageMenu), "Open", new[] { typeof(StorageEntity), typeof(Il2CppSystem.Action) });
            HarmonyInstance.Patch(open, new HarmonyMethod(typeof(Mod), nameof(Opening)), new HarmonyMethod(typeof(Mod), nameof(Opened)));
            Patch(typeof(StorageMenu), "OnClose", postfix: nameof(Closed));
            LoggerInstance.Msg(settings.Text("ESB_READY | 0.2.0-beta.2 | Experimentelle Multiplayer-Beta.", "ESB_READY | 0.2.0-beta.2 | Experimental multiplayer beta."));
            settings.Trace("ESB_READY | 0.2.0-beta.2 | experimental; two-client core tests confirmed");
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

    private static void VisualizerStarted() => Run(storage => storage.Request());

    private static bool Active => instance != null && !instance.disabled;

    // Runtime UI/storage failures must not bypass inventory persistence. Keep
    // reading/writing the backpack snapshot even when interactive updates stop.
    private static bool PersistenceActive => instance != null && !InstanceFinder.IsClientOnly;

    private static void Run(Action<RackStorage> action)
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
        if (disabled) { RefreshSettingsUi(); return; }
        steamSettings?.Tick();
        nativeChannel?.Tick();
        remoteBackpack?.Tick();
        RefreshSettingsUi();
        if (!Active) return;
        if (!InstanceFinder.IsClientOnly) backpack?.Tick();
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
            var phone = Read("ModManagerPanel") as UnityEngine.GameObject;
            var content = Read("rightPanelContent") as UnityEngine.Transform;
            bool jsonEditorOpen = false;
            if (content != null)
                for (int i = 0; i < content.childCount; i++)
                {
                    var child = content.GetChild(i);
                    if (child.name.StartsWith("JsonEditor_", StringComparison.Ordinal) &&
                        child.gameObject.activeSelf) jsonEditorOpen = true;
                }
            // Manager 2.2.4's public refresh mistakes its inactive JSON template
            // for an open editor. Rebuild only our selected phone settings page.
            if (ReferenceEquals(selected, this) && phone != null &&
                phone.activeInHierarchy && content != null && !jsonEditorOpen)
            {
                var populate = managerType.GetMethod("PopulateModSettings",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(MelonBase) }, null);
                if (populate != null)
                {
                    populate.Invoke(manager, new object[] { this });
                    settings?.Trace("ESB_MANAGER_REFRESH | own phone settings rebuilt");
                    return;
                }
            }
            refresh.Invoke(manager, null);
            settings?.Trace("ESB_MANAGER_REFRESH | requested");
        }
        catch (Exception ex) { settings?.Error("ESB_MANAGER_REFRESH", ex); }
    }

    private static void StorageStarted(StorageEntity __instance) => Run(s => s.Started(__instance));
    private static void Placed(PlaceableStorageEntity __instance) => Run(s => s.Started(__instance.StorageEntity));
    private static void Destroyed(StorageEntity __instance) => Run(s => s.Removed(__instance));
    private static void ContentsChanged(StorageEntity __instance) => Run(s => s.ContentsChanged(__instance));
    private static bool Opening(StorageEntity __0)
    {
        if (InstanceFinder.IsClientOnly && instance?.storage?.Supports(__0) == true &&
            instance.remoteBackpack?.CanOpenStorage(__0) != true) return false;
        Run(s => s.BeforeOpen(__0)); return true;
    }
    private static void Opened(StorageEntity __0)
    {
        if (InstanceFinder.IsClientOnly && instance?.storage?.Supports(__0) == true &&
            instance.remoteBackpack?.CanOpenStorage(__0) != true) return;
        Run(s => s.AfterOpen(__0));
    }
    private static void Closed()
    {
        instance?.backpack?.Closed();
        instance?.remoteBackpack?.Closed();
        Run(s => s.Closed());
    }
    private static void GameStarting()
    {
        instance?.remoteBackpack?.Reset();
        instance?.backpack?.Reset();
        instance?.multiplayer?.Reset();
        instance?.steamSettings?.Reset();
        instance?.nativeChannel?.Reset();
        instance?.remoteSaves.Reset();
        instance?.remoteJournal?.Reset();
    }
    private static void NetworkPlayerStarted(Player __instance)
        => instance?.multiplayer?.Observe("player-start", __instance);
    private static void NetworkSaveRequested(Player __instance)
        => instance?.multiplayer?.Observe("save-request-send", __instance);
    private static void NetworkSaveReceived(Player __instance)
        => instance?.multiplayer?.Observe("save-request-server", __instance);
    private static void NetworkPlayerReceiving(Player __instance, ref string __2)
        => instance?.multiplayer?.Incoming(__instance, ref __2);
    private static bool LocalPlayer(Player player) => player.IsLocalPlayer ||
        (Player.Local != null && Player.Local.Pointer == player.Pointer);
    private static void TraceSaveBoundary(string stage, Player? player = null)
    {
        instance?.multiplayer?.Observe(stage, player);
        // Diagnostics must never interrupt the game's save pipeline.
        try
        {
            if (instance?.settings == null || !instance.settings.DebugLogging.Value) return;
            instance.settings.Trace($"ESB_SAVE_PATH | stage={stage} | disabled={instance.disabled} | clientOnly={InstanceFinder.IsClientOnly} | playerPresent={player != null} | local={(player != null && LocalPlayer(player))} | backpack={instance.backpack?.DiagnosticState ?? "missing"}");
        }
        catch (Exception) { /* Diagnostic observation only; no persistence action. */ }
    }
    private static void SaveRequested() => TraceSaveBoundary("SaveManager.Save");
    private static void PlayerWriteStarting(Player __instance) => TraceSaveBoundary("Player.WriteData.begin", __instance);
    private static void PlayerWriteFinished(Player __instance) => TraceSaveBoundary("Player.WriteData.end", __instance);
    private static void ManagerWriteStarting()
    {
        TraceSaveBoundary("PlayerManager.WriteData.begin");
        if (!PersistenceActive || !InstanceFinder.IsServer) return;
        // Before WriteData adds new players to loadedPlayerData; otherwise a new
        // player would look like an existing save with a missing Inventory file.
        try
        {
            foreach (var player in Player.PlayerList)
                if (player != null && !LocalPlayer(player)) instance!.EnsureRemoteBaseline(player);
        }
        catch (Exception ex)
        {
            SaveManager.ReportSaveError();
            instance!.settings!.Error("ESB_REMOTE_SAVE_BASELINE", ex);
            throw;
        }
    }
    private static void ManagerWriteFinished() => TraceSaveBoundary("PlayerManager.WriteData.end");
    private static void ManagerPlayerSaving(Player __0)
    {
        TraceSaveBoundary("PlayerManager.SavePlayer", __0);
        if (!PersistenceActive || !InstanceFinder.IsServer || LocalPlayer(__0)) return;
        try { instance!.EnsureRemoteBaseline(__0); }
        catch (Exception ex)
        {
            SaveManager.ReportSaveError();
            instance!.settings!.Error("ESB_REMOTE_SAVE_BASELINE", ex);
            throw;
        }
    }
    private static void InventorySaving(Player __instance, ref string __result)
    {
        TraceSaveBoundary("Player.GetInventoryString.return", __instance);
        if (!PersistenceActive || instance?.backpack == null) return;
        try { __result = LocalPlayer(__instance) ? instance.backpack.WriteInventory(__result)
            : instance.PreserveRemoteInventory(__instance, __result); }
        catch (Exception ex)
        {
            SaveManager.ReportSaveError();
            instance.settings!.Error("ESB_BACKPACK_SAVE", ex);
            throw; // Never silently save only the hotbar after a failed backpack snapshot.
        }
    }
    private static bool InventorySubfileWriting(ISaveable __instance, string __1, ref string __2)
    {
        // Native Player.WriteData can bypass the GetInventoryString detour.
        // Attach at the actual writer boundary, before the game's own file write.
        // Restrict this shared API to player Inventory subfiles.
        if (__1 != "Inventory") return true;
        var player = __instance.TryCast<Player>();
        instance?.multiplayer?.Observe("inventory-subfile-write", player, __2.Length);
        if (!PersistenceActive || instance?.backpack == null) return true;
        if (player == null) return true;
        try
        {
            if (instance.remoteJournal?.DeferInventoryWrite(player) == true) return false;
            __2 = LocalPlayer(player) ? instance.backpack.WriteInventory(__2)
                : instance.PreserveRemoteInventory(player, __2);
            TraceSaveBoundary("Player.Inventory.WriteSubfile", player);
            return true;
        }
        catch (Exception ex)
        {
            SaveManager.ReportSaveError();
            instance.settings!.Error("ESB_BACKPACK_SUBFILE_SAVE", ex);
            throw;
        }
    }
    private string ReadRemoteSnapshot(Player player)
    {
        if (!InstanceFinder.IsServer || LocalPlayer(player))
            throw new InvalidOperationException("Only a remote owner's host inventory may be exported.");
        // The native getter carries the current host hotbar; the preservation cache
        // supplies only this player's host-saved backpack. No inventory is restored or written.
        string snapshot = PreserveRemoteInventory(player, player.GetInventoryString());
        remoteJournal!.Admit(player, snapshot);
        return snapshot;
    }
    private string PreserveRemoteInventory(Player player, string inventory)
    {
        if (!InstanceFinder.IsServer) return inventory;
        if (remoteJournal != null && remoteJournal.PendingInventory(player, out string pendingInventory)) return pendingInventory;
        EnsureRemoteBaseline(player);
        return remoteSaves.Preserve(player.PlayerCode, inventory);
    }
    private void EnsureRemoteBaseline(Player player)
    {
        string code = player.PlayerCode;
        if (!remoteSaves.Contains(code))
        {
            if (!PlayerManager.InstanceExists)
                throw new InvalidOperationException("PlayerManager unavailable for remote backpack preservation.");
            bool found = PlayerManager.Instance.TryGetPlayerData(code, out var data, out string saved,
                out var appearance, out var clothing, out var variables);
            // The native method also returns true when a player exists but reading
            // Inventory failed. Treat that as a save error, never an empty backpack.
            if (found && string.IsNullOrWhiteSpace(saved))
                throw new InvalidDataException("Existing remote player inventory could not be read.");
            remoteSaves.Load(code, found ? saved : "{}");
            settings!.Trace("ESB_REMOTE_SAVE_BASELINE | host save inspected; identity omitted");
        }
    }
    private static void InventoryLoading(Player __instance, ref string __0)
    {
        TraceSaveBoundary("Player.LoadInventory.begin", __instance);
        if (!PersistenceActive || !LocalPlayer(__instance) || instance?.backpack == null) return;
        try { instance.backpack.ReadInventory(ref __0); }
        catch (Exception ex)
        {
            instance.settings!.Error("ESB_BACKPACK_LOAD", ex);
            throw;
        }
    }
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
        remoteBackpack?.Dispose();
        nativeChannel?.Dispose();
        remoteJournal?.Dispose();
        steamSettings?.Dispose();
        backpack?.Dispose();
        storage?.Dispose();
        if (settings != null) settings.LanguageChanged -= RequestSettingsRefresh;
        settings?.Dispose();
        instance = null;
    }
}
