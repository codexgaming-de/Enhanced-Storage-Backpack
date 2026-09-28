using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using UnityEngine;
using System.Text.Json;

namespace EnhancedStorageBackpack;

internal sealed class Backpack : IDisposable
{
    private readonly Settings settings;
    private readonly BackpackInput input = new();
    private readonly BackpackMenu menu;
    private BackpackOwner? owner;
    private string? savedPayload;
    private bool loadFault;
    private bool ready, failed, pending = true, wasLoaded;
    private readonly Il2CppSystem.Action changed;

    internal string DiagnosticState => $"ready:{ready},failed:{failed},loadFault:{loadFault},payload:{savedPayload != null},slots:{owner?.ItemSlots.Count ?? 0},occupied:{owner?.GetNonEmptySlotCount() ?? 0}";

    public Backpack(Settings settings)
    {
        this.settings = settings;
        menu = new BackpackMenu(settings);
        changed = (Il2CppSystem.Action)Request;
        ClassInjector.RegisterTypeInIl2Cpp<BackpackOwner>(new RegisterTypeOptions
        { Interfaces = new[] { typeof(IItemSlotOwner) } });
        settings.BackpackChanged += Request;
        settings.LanguageChanged += RefreshLanguage;
    }
    private void Request() => pending = true;
    private void RefreshLanguage() { pending = true; }
    public void Reset()
    {
        menu.Close();
        owner = null;
        savedPayload = null;
        ready = failed = wasLoaded = loadFault = false;
        pending = true;
    }
    public void Closed() => menu.Closed();
    public void ReadInventory(ref string inventory)
    {
        // Replace the session state on each native inventory restore, even for
        // vanilla saves without our key. Never carry items across save slots.
        menu.Close();
        owner = null;
        ready = failed = false;
        loadFault = true;
        try { savedPayload = BackpackSave.Extract(ref inventory); loadFault = false; }
        catch { failed = true; throw; }
        pending = true;
        settings.Trace($"ESB_BACKPACK_LOAD_PAYLOAD | present={savedPayload != null}");
    }
    public string WriteInventory(string inventory)
    {
        // This method only creates a string; the game decides when to persist it.
        // Preserve the original payload if an item could not be restored.
        if (loadFault) throw new InvalidDataException("Cannot save after a failed backpack inventory load.");
        string? payload = savedPayload;
        if (ready && owner != null)
        {
            var items = new string?[owner.ItemSlots.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = owner.ItemSlots[i].ItemInstance?.GetItemData().GetJson(false);
            payload = BackpackSave.Encode(items);
        }
        if (payload == null) return inventory;
        string result = BackpackSave.Attach(inventory, payload);
        settings.Trace($"ESB_BACKPACK_SERIALIZE | slots={owner?.ItemSlots.Count ?? 0} | restored={ready}");
        return result;
    }
    private void Grow(BackpackOwner target, int count)
    {
        var nativeOwner = target.Cast<IItemSlotOwner>();
        while (target.ItemSlots.Count < count)
        {
            var slot = new ItemSlot(false);
            int before = target.ItemSlots.Count;
            slot.SetSlotOwner(nativeOwner);
            if (target.ItemSlots.Count == before) target.ItemSlots.Add(slot);
            if (target.ItemSlots.Count != before + 1 || target.ItemSlots[before].Pointer != slot.Pointer)
                throw new InvalidOperationException("Unexpected backpack slot registration.");
            slot.onItemDataChanged += changed;
        }
    }
    private void Restore()
    {
        var items = savedPayload == null ? Array.Empty<string?>() : BackpackSave.Decode(savedPayload);
        var restored = new BackpackOwner();
        Grow(restored, Math.Max(Math.Clamp(settings.EffectiveBackpackSlots, 1, 128), items.Length));
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            using var json = JsonDocument.Parse(items[i]!);
            string type = json.RootElement.GetProperty("DataType").GetString()
                ?? throw new InvalidDataException("Item has no data type.");
            var loader = LoadManager.Instance.GetItemLoader(type)
                ?? throw new InvalidDataException($"No item loader for {type}.");
            var item = loader.LoadItem(items[i]!)
                ?? throw new InvalidDataException("Backpack item could not be restored.");
            restored.ItemSlots[i].SetStoredItem(item, true);
        }
        owner = restored;
        ready = true;
        pending = true;
        settings.Trace($"ESB_BACKPACK_READY | slots={owner.ItemSlots.Count} | occupied={owner.GetNonEmptySlotCount()}");
    }
    private static bool Protected(ItemSlot slot) => slot.IsLocked || slot.IsAddLocked || slot.IsRemovalLocked ||
        slot.SiblingSet != null || (slot.PlayerFilter != null && !slot.PlayerFilter.IsDefault());
    private void Resize()
    {
        var slots = owner!.ItemSlots;
        int target = Math.Clamp(settings.EffectiveBackpackSlots, 1, 128);
        if (slots.Count != target)
        {
            menu.ClearBindings();
            for (int i = target; i < slots.Count; i++)
            {
                if (slots[i].ItemInstance == null || Protected(slots[i])) continue;
                for (int j = 0; j < target; j++)
                {
                    if (slots[j].ItemInstance != null || Protected(slots[j])) continue;
                    var empty = slots[j]; slots[j] = slots[i]; slots[i] = empty;
                    break;
                }
            }
            int actual = StorageRules.SafeSize(target, slots.Count, i => slots[i].ItemInstance != null || Protected(slots[i]));
            Grow(owner, actual);
            for (int i = slots.Count - 1; i >= actual; i--)
            {
                var slot = slots[i];
                slot.onItemDataChanged -= changed;
                slots.RemoveAt(i);
                slot._SlotOwner_k__BackingField = null!;
            }
            settings.Trace($"ESB_BACKPACK_RESIZE | requested={target} | actual={slots.Count}");
        }
        menu.Bind();
    }
    public void Tick()
    {
        bool loaded = LoadManager.InstanceExists && LoadManager.Instance.IsGameLoaded && !LoadManager.Instance.IsLoading;
        if (!loaded)
        {
            if (wasLoaded) Reset();
            return;
        }
        wasLoaded = true;
        if (failed || Player.Local == null) return;
        if (SaveManager.InstanceExists && SaveManager.Instance.IsSaving) return;
        try
        {
            if (!ready) Restore();
            if (pending && !RackStorage.Dragging) { pending = false; Resize(); }
            try { menu.LayoutTick(); }
            catch (Exception ex) { settings.Error("ESB_BACKPACK_LAYOUT", ex); }
            if (Il2CppScheduleOne.GameInput.IsTyping) return;
            if (!input.Pressed(settings.BackpackHotkey.Value)) return;
            if (RackStorage.Dragging) return;
            if (menu.IsOpen) { menu.Close(); return; }
            var player = Player.Local;
            if (Cursor.visible || player.IsArrested || player.IsUnconscious || player.IsSleeping ||
                player.IsTased || player.IsRagdolled || player.IsInVehicle ||
                !StorageMenu.InstanceExists || StorageMenu.Instance.IsOpen) return;
            menu.Open(owner!);
            settings.Trace("ESB_BACKPACK_OPEN");
        }
        catch (Exception ex)
        {
            failed = true;
            menu.Close();
            settings.Error("ESB_BACKPACK_DISABLED", ex);
        }
    }
    public void Dispose()
    {
        settings.BackpackChanged -= Request;
        settings.LanguageChanged -= RefreshLanguage;
        menu.Dispose();
        owner = null;
    }
}
