using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;

namespace EnhancedStorageBackpack;

public enum ModLanguage { Deutsch, English }

internal sealed class Settings : IDisposable
{
    // Mod Manager matches the MelonInfo name with spaces removed.
    private const string Prefix = "EnhancedStorage+Backpack";
    private readonly List<(MelonPreferences_Entry Entry, LemonAction<object, object> Handler)> subscriptions = new();
    private readonly MelonLogger.Instance logger;
    private readonly DiagnosticLog diagnostics;

    public MelonPreferences_Entry<ModLanguage> Language { get; }
    public MelonPreferences_Entry<bool> DebugLogging { get; }
    public MelonPreferences_Entry<int> BackpackSlots { get; }
    public MelonPreferences_Entry<KeyCode> BackpackHotkey { get; }

    public Settings(MelonLogger.Instance logger)
    {
        this.logger = logger;
        diagnostics = new DiagnosticLog(logger);
        var general = Category("01_General", "Allgemein / General");
        Language = general.CreateEntry("Language", ModLanguage.Deutsch, "Sprache / Language");
        DebugLogging = general.CreateEntry("DebugLogging", false, "Diagnose-Log / Debug logging");
        var backpack = Category("02_Backpack", "Rucksack / Backpack");
        BackpackSlots = backpack.CreateEntry("Slots", 40, "Slots (1–128)",
            "Rucksackgröße / Backpack size", validator: new ValueRange<int>(1, 128));
        BackpackHotkey = backpack.CreateEntry("Hotkey", KeyCode.B, "Öffnen / Open backpack");

        var storageNames = new[]
        {
            ("SmallStorageRack", "Small Storage Rack"),
            ("MediumStorageRack", "Medium Storage Rack"),
            ("LargeStorageRack", "Large Storage Rack"),
            ("SmallStorageCloset", "Small Storage Closet"),
            ("MediumStorageCloset", "Medium Storage Closet"),
            ("LargeStorageCloset", "Large Storage Closet"),
            ("HugeStorageCloset", "Huge Storage Closet"),
            ("Safe", "Safe"),
            ("FilingCabinet", "Filing Cabinet")
        };

        // Register and load every entry before subscribing, so callbacks see complete settings.
        var entries = new List<MelonPreferences_Entry> { Language, DebugLogging, BackpackSlots, BackpackHotkey };
        for (int i = 0; i < storageNames.Length; i++)
        {
            var (id, name) = storageNames[i];
            var category = Category($"{i + 3:00}_{id}", name);
            entries.Add(category.CreateEntry("Slots", 0, "Slots (0 = Original, 1–128)",
                "0 behält die Spielvorgabe / 0 keeps the game's default", validator: new ValueRange<int>(0, 128)));
            entries.Add(category.CreateEntry("Rows", 0, "Reihen / Rows (0 = Original, 1–128)",
                "Vorläufige technische Grenze; Darstellung wird im Storage-Schritt geprüft. / Provisional bound; layout validation follows in the storage step.",
                validator: new ValueRange<int>(0, 128)));
        }

        foreach (var entry in entries)
        {
            LemonAction<object, object> handler = (oldValue, newValue) => OnChanged(entry, oldValue, newValue);
            entry.OnEntryValueChangedUntyped.Subscribe(handler);
            subscriptions.Add((entry, handler));
            // Startup values make persistence checkable after restarting the game.
            logger.Msg($"ESB_SETTING_LOADED | {entry.Category.Identifier}/{entry.Identifier} = {entry.GetValueAsString()}");
        }
        WriteDiagnosticSnapshot();
    }

    private static MelonPreferences_Category Category(string suffix, string label)
        => MelonPreferences.CreateCategory($"{Prefix}_{suffix}", label);

    private void OnChanged(MelonPreferences_Entry entry, object oldValue, object newValue)
    {
        // No game-save operation belongs here: preferences and inventory contents are separate.
        var message = $"ESB_SETTING_CHANGED | {entry.Category.Identifier}/{entry.Identifier}: {oldValue} -> {newValue}";
        logger.Msg(message);
        diagnostics.Write(DebugLogging.Value, message);
        if (ReferenceEquals(entry, DebugLogging) && DebugLogging.Value)
            WriteDiagnosticSnapshot();
    }

    private void WriteDiagnosticSnapshot()
    {
        if (!DebugLogging.Value) return;
        diagnostics.Write(true, "ESB_SETTINGS_SNAPSHOT | 0.0.3");
        foreach (var (entry, _) in subscriptions)
            diagnostics.Write(true, $"ESB_SETTING_CURRENT | {entry.Category.Identifier}/{entry.Identifier} = {entry.GetValueAsString()}");
    }

    public void Dispose()
    {
        foreach (var (entry, handler) in subscriptions)
            entry.OnEntryValueChangedUntyped.Unsubscribe(handler);
        subscriptions.Clear();
    }
}
