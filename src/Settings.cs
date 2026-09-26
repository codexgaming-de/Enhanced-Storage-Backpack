using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;

namespace EnhancedStorageBackpack;

public enum ModLanguage { Deutsch, English }

internal sealed class Settings : IDisposable
{
    private const string Prefix = "EnhancedStorage+Backpack";
    private readonly List<(MelonPreferences_Entry Entry, LemonAction<object, object> Handler)> subscriptions = new();
    private readonly List<(Action<string> Set, string German, string English)> labels = new();
    private readonly MelonLogger.Instance logger;
    private readonly DiagnosticLog diagnostics;

    public MelonPreferences_Entry<ModLanguage> Language { get; }
    public MelonPreferences_Entry<bool> DebugLogging { get; }
    public MelonPreferences_Entry<int> BackpackSlots { get; }
    public MelonPreferences_Entry<KeyCode> BackpackHotkey { get; }
    public MelonPreferences_Entry<int> SmallRackSlots { get; private set; } = null!;
    public MelonPreferences_Entry<int> SmallRackRows { get; private set; } = null!;
    public MelonPreferences_Entry<int> MediumRackSlots { get; private set; } = null!;
    public MelonPreferences_Entry<int> MediumRackRows { get; private set; } = null!;
    public event Action? RackChanged;
    public event Action? LanguageChanged;

    public Settings(MelonLogger.Instance logger)
    {
        this.logger = logger;
        diagnostics = new DiagnosticLog(logger);
        var general = Category("01_General", "Allgemein", "General");
        Language = Entry(general, "Language", ModLanguage.Deutsch, "Sprache", "Language");
        DebugLogging = Entry(general, "DebugLogging", false, "Diagnoseprotokoll", "Debug logging");
        var backpack = Category("02_Backpack", "Rucksack", "Backpack");
        BackpackSlots = Entry(backpack, "Slots", 40, "Plätze (1–128)", "Slots (1–128)", 1, 128);
        BackpackHotkey = Entry(backpack, "Hotkey", KeyCode.B, "Taste zum Öffnen", "Open key");

        var types = new[]
        {
            ("SmallStorageRack", "Kleines Lagerregal", "Small Storage Rack"),
            ("MediumStorageRack", "Mittleres Lagerregal", "Medium Storage Rack"),
            ("LargeStorageRack", "Großes Lagerregal", "Large Storage Rack"),
            ("SmallStorageCloset", "Kleiner Lagerschrank", "Small Storage Closet"),
            ("MediumStorageCloset", "Mittlerer Lagerschrank", "Medium Storage Closet"),
            ("LargeStorageCloset", "Großer Lagerschrank", "Large Storage Closet"),
            ("HugeStorageCloset", "Riesiger Lagerschrank", "Huge Storage Closet"),
            ("Safe", "Tresor", "Safe"),
            ("FilingCabinet", "Aktenschrank", "Filing Cabinet")
        };
        for (int index = 0; index < types.Length; index++)
        {
            var (id, german, english) = types[index];
            var category = Category($"{index + 3:00}_{id}", german, english);
            var slots = Entry(category, "Slots", 0, "Plätze (0 = Spielvorgabe, 1–128)", "Slots (0 = default, 1–128)", 0, 128);
            var rows = Entry(category, "Rows", 0, "Reihen (0 = Spielvorgabe)", "Rows (0 = default)", 0, 128);
            if (index == 0) { SmallRackSlots = slots; SmallRackRows = rows; }
            if (index == 1) { MediumRackSlots = slots; MediumRackRows = rows; }
        }
        ApplyLanguage();
        foreach (var (entry, handler) in subscriptions)
        {
            entry.OnEntryValueChangedUntyped.Subscribe(handler);
            logger.Msg($"ESB_SETTING_LOADED | {entry.Category.Identifier}/{entry.Identifier} = {entry.GetValueAsString()}");
        }
        WriteDiagnosticSnapshot();
    }

    private MelonPreferences_Category Category(string suffix, string german, string english)
    {
        var category = MelonPreferences.CreateCategory($"{Prefix}_{suffix}", german);
        labels.Add((value => category.DisplayName = value, german, english));
        return category;
    }

    private MelonPreferences_Entry<T> Entry<T>(MelonPreferences_Category category, string id, T initial,
        string german, string english, int? min = null, int? max = null)
    {
        var entry = category.CreateEntry(id, initial, german,
            validator: min.HasValue && max.HasValue ? new ValueRange<int>(min.Value, max.Value) : null);
        labels.Add((value => entry.DisplayName = value, german, english));
        var (descriptionGerman, descriptionEnglish) = Describe(category.Identifier, id);
        labels.Add((value => entry.Description = value, descriptionGerman, descriptionEnglish));
        LemonAction<object, object> handler = (oldValue, newValue) => OnChanged(entry, oldValue, newValue);
        subscriptions.Add((entry, handler));
        return entry;
    }

    private static (string German, string English) Describe(string category, string id)
    {
        if (id == "Language") return ("Wählt die Sprache der Einstellungen dieses Plugins.", "Selects the language of this plugin's settings.");
        if (id == "DebugLogging") return ("Schreibt Diagnosemeldungen nach UserData/Enhanced-Storage-Backpack-Debug.log.", "Writes diagnostics to UserData/Enhanced-Storage-Backpack-Debug.log.");
        if (id == "Hotkey") return ("Taste für den Rucksack. Die Rucksackfunktion folgt in einem späteren Entwicklungsschritt.", "Key for opening the backpack. Backpack functionality will follow in a later development step.");
        if (category.EndsWith("_Backpack")) return ("Gewünschte Rucksackgröße von 1 bis 128 Plätzen. Die Rucksackfunktion ist noch nicht aktiv.", "Requested backpack capacity from 1 to 128 slots. Backpack functionality is not active yet.");
        bool implemented = category.EndsWith("_SmallStorageRack") || category.EndsWith("_MediumStorageRack");
        string german = id == "Slots"
            ? "Anzahl der Plätze: 1 bis 128. 0 verwendet die Spielvorgabe. Belegte Plätze bleiben beim Verkleinern erhalten."
            : "Anzahl der angezeigten Reihen. 0 verwendet die Spielvorgabe. Höchstens so viele Reihen wie Plätze.";
        string english = id == "Slots"
            ? "Number of slots: 1 to 128. 0 uses the game default. Occupied slots are retained when reducing capacity."
            : "Number of displayed rows. 0 uses the game default. Cannot exceed the number of slots.";
        if (!implemented)
        {
            german += " Für diesen Lagertyp noch ohne Wirkung.";
            english += " Not active for this storage type yet.";
        }
        return (german, english);
    }

    public string Text(string german, string english) => Language.Value == ModLanguage.English ? english : german;
    public void Trace(string message) => diagnostics.Write(DebugLogging.Value, message);
    public void Error(string context, Exception exception)
    {
        logger.Error($"{context}: {exception.Message}");
        Trace($"ERROR | {context} | {exception}");
    }

    private void ApplyLanguage()
    {
        foreach (var (set, german, english) in labels) set(Text(german, english));
        Trace($"ESB_LANGUAGE_APPLIED | language={Language.Value} | label={Language.DisplayName} | description={Language.Description}");
    }

    private void OnChanged(MelonPreferences_Entry entry, object oldValue, object newValue)
    {
        var message = $"ESB_SETTING_CHANGED | {entry.Category.Identifier}/{entry.Identifier}: {oldValue} -> {newValue}";
        logger.Msg(message);
        Trace(message);
        if (ReferenceEquals(entry, Language)) { ApplyLanguage(); LanguageChanged?.Invoke(); }
        if (ReferenceEquals(entry, SmallRackSlots) || ReferenceEquals(entry, SmallRackRows) ||
            ReferenceEquals(entry, MediumRackSlots) || ReferenceEquals(entry, MediumRackRows)) RackChanged?.Invoke();
        if (ReferenceEquals(entry, DebugLogging) && DebugLogging.Value) WriteDiagnosticSnapshot();
    }

    private void WriteDiagnosticSnapshot()
    {
        if (!DebugLogging.Value) return;
        Trace("ESB_SETTINGS_SNAPSHOT | 0.0.7");
        foreach (var (entry, _) in subscriptions)
            Trace($"ESB_SETTING_CURRENT | {entry.Category.Identifier}/{entry.Identifier} = {entry.GetValueAsString()}");
    }

    public void Dispose()
    {
        foreach (var (entry, handler) in subscriptions) entry.OnEntryValueChangedUntyped.Unsubscribe(handler);
        subscriptions.Clear();
        RackChanged = null;
        LanguageChanged = null;
    }
}
