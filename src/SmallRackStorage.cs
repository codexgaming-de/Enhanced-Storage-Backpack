using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Items;
using UnityEngine;

namespace EnhancedStorageBackpack;

internal sealed class SmallRackStorage : IDisposable
{
    private sealed class Rack
    {
        public readonly StorageEntity Entity;
        public readonly int OriginalSlots;
        public readonly int OriginalRows;
        public bool Failed;
        public int LastBlockedTarget = -1;
        public Rack(StorageEntity entity)
        {
            Entity = entity;
            OriginalSlots = entity.SlotCount;
            OriginalRows = entity.DisplayRowCount;
        }
    }

    private readonly Dictionary<IntPtr, Rack> racks = new();
    private readonly Settings settings;
    private readonly RackMenu menu;
    private bool pending;
    private bool applying;
    public SmallRackStorage(Settings settings)
    {
        this.settings = settings;
        menu = new RackMenu(settings);
        settings.SmallRackChanged += Request;
        settings.LanguageChanged += Request;
    }

    public static bool Dragging => ItemUIManager.InstanceExists &&
        (ItemUIManager.Instance.IsCurrentlyDragging || ItemUIManager.Instance.IsDraggingCash);

    private static bool SaveOrLoadInProgress =>
        (LoadManager.InstanceExists && LoadManager.Instance.IsLoading) ||
        (SaveManager.InstanceExists && SaveManager.Instance.IsSaving);

    public void Request() => pending = true;

    private Rack? Track(StorageEntity entity)
    {
        if (entity == null) return null;
        if (racks.TryGetValue(entity.Pointer, out var known)) return known;
        var placeable = entity.GetComponentInParent<PlaceableStorageEntity>();
        var definition = placeable?.ItemInstance?.Definition;
        // Prefer immutable item identity over a player-editable storage label.
        // Placement previews have no item definition and must not be resized.
        bool match = definition != null && Normalize(definition.ID) == "smallstoragerack";
        if (!match) return null;
        var rack = new Rack(entity);
        racks.Add(entity.Pointer, rack);
        settings.Trace($"ESB_RACK_FOUND | id={definition?.ID} | name={entity.StorageEntityName} | originalSlots={rack.OriginalSlots} | originalRows={rack.OriginalRows}");
        return rack;
    }

    private static string Normalize(string? value)
        => new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public void Started(StorageEntity entity) { if (Track(entity) != null) Request(); }
    public void Removed(StorageEntity entity) => racks.Remove(entity.Pointer);

    public void ContentsChanged(StorageEntity entity)
    {
        if (applying || !racks.TryGetValue(entity.Pointer, out var rack) || rack.Failed) return;
        if (entity.ItemSlots.Count != StorageRules.TargetSlots(settings.SmallRackSlots.Value, rack.OriginalSlots)) Request();
    }

    public void BeforeLoad(StorageEntity entity, Il2CppReferenceArray<ItemInstance> items)
    {
        var rack = Track(entity);
        if (rack == null || rack.Failed) return;
        // The native loader may index every serialized slot, including empty trailing ones.
        // Grow first; never shrink until after native restoration has finished.
        Grow(entity, Math.Max(entity.ItemSlots.Count, items.Length));
        settings.Trace($"ESB_RACK_LOAD | serializedSlots={items.Length} | availableSlots={entity.ItemSlots.Count}");
        Request();
    }

    private static void Grow(StorageEntity entity, int size)
    {
        var slots = entity.ItemSlots;
        if (slots.Count >= size) return;
        var owner = entity.Cast<IItemSlotOwner>();
        Il2CppSystem.Action contentsChanged = (Il2CppSystem.Action)entity.ContentsChanged;
        while (slots.Count < size)
        {
            var slot = new ItemSlot(entity.SlotsAreFilterable);
            int countBefore = slots.Count;
            slot.SetSlotOwner(owner);
            // Native SetSlotOwner can register the slot itself. Never append it twice.
            if (slots.Count == countBefore) slots.Add(slot);
            if (slots.Count != countBefore + 1 || slots[slots.Count - 1].Pointer != slot.Pointer)
                throw new InvalidOperationException("Unexpected native slot registration.");
            if (countBefore > 0)
            {
                var filters = slots[0].HardFilters;
                for (int i = 0; i < filters.Count; i++) slot.AddFilter(filters[i]);
            }
            slot.onItemDataChanged += contentsChanged;
        }
        entity.SlotCount = slots.Count;
    }

    private void Apply(Rack rack)
    {
        var entity = rack.Entity;
        if (entity == null || rack.Failed || entity.ItemSlots == null || entity.ItemSlots.Count == 0) return;
        var slots = entity.ItemSlots;
        int before = slots.Count;
        int target = StorageRules.TargetSlots(settings.SmallRackSlots.Value, rack.OriginalSlots);
        int size = StorageRules.SafeSize(target, before, i =>
            slots[i].ItemInstance != null || slots[i].IsLocked || slots[i].IsRemovalLocked ||
            slots[i].IsAddLocked || (slots[i].PlayerFilter != null && !slots[i].PlayerFilter.IsDefault()) || slots[i].SiblingSet != null);
        applying = true;
        try
        {
            // Clear displayed bindings before releasing an empty tail of slots.
            if (size < before && menu.IsShowing(entity)) menu.ClearBindings();
            Grow(entity, size);
            Il2CppSystem.Action contentsChanged = (Il2CppSystem.Action)entity.ContentsChanged;
            for (int i = slots.Count - 1; i >= size; i--)
            {
                var slot = slots[i];
                slot.onItemDataChanged -= contentsChanged;
                slots.RemoveAt(i);
                // SetSlotOwner dereferences its owner argument; null is not supported.
                slot._SlotOwner_k__BackingField = null!;
            }
            entity.SlotCount = slots.Count;
            if (before != slots.Count) entity.ContentsChanged();
            int rows = StorageRules.Rows(settings.SmallRackRows.Value, rack.OriginalRows, target);
            if (slots.Count > target)
            {
                // Retained slots must not make the requested layout wider.
                // Derive this from the requested size, not the previous layout,
                // so changing rows before capacity gives the same result.
                int targetColumns = (target + rows - 1) / rows;
                rows = Math.Max(rows, (slots.Count + targetColumns - 1) / targetColumns);
            }
            bool changed = before != slots.Count || entity.DisplayRowCount != rows;
            entity.DisplayRowCount = rows;
            if (changed) settings.Trace($"ESB_RACK_APPLIED | requested={target} | actual={slots.Count} | rows={rows}");
            if (size > target && rack.LastBlockedTarget != target)
                settings.Trace($"ESB_RACK_SHRINK_DEFERRED | requested={target} | protectedSize={size}");
            rack.LastBlockedTarget = size > target ? target : -1;
            if (menu.IsShowing(entity)) menu.Bind(entity);
        }
        finally { applying = false; }
    }

    public void Tick()
    {
        if (!pending || SaveOrLoadInProgress || Dragging) return;
        pending = false;
        foreach (var rack in racks.Values.ToArray())
        {
            try { Apply(rack); }
            catch (Exception ex) { rack.Failed = true; settings.Error("ESB_RACK_UPDATE", ex); }
        }
    }

    public void BeforeOpen(StorageEntity entity)
    {
        menu.Restore();
        var rack = Track(entity);
        settings.Trace($"ESB_STORAGE_OPEN | name={entity.StorageEntityName} | smallRack={rack != null} | slots={entity.ItemSlots.Count}");
        if (rack == null || rack.Failed) return;
        if (!SaveOrLoadInProgress && !Dragging) Apply(rack);
        else Request();
        menu.Prepare(entity.ItemSlots.Count);
    }

    public void AfterOpen(StorageEntity entity)
    {
        if (racks.TryGetValue(entity.Pointer, out var rack) && !rack.Failed) menu.Bind(entity);
    }

    public void Closed() { menu.Restore(); Request(); }

    public void Dispose()
    {
        settings.SmallRackChanged -= Request;
        settings.LanguageChanged -= Request;
        menu.Restore();
        racks.Clear();
    }
}
