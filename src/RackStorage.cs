using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Items;
using UnityEngine;

namespace EnhancedStorageBackpack;

internal sealed class RackStorage : IDisposable
{
    private sealed class Rack
    {
        public readonly StorageEntity Entity;
        public readonly string ItemId;
        public readonly int OriginalSlots;
        public readonly int OriginalRows;
        public bool Failed;
        public int LastBlockedTarget = -1;
        public Rack(StorageEntity entity, string itemId)
        {
            Entity = entity;
            ItemId = itemId;
            OriginalSlots = entity.SlotCount;
            OriginalRows = entity.DisplayRowCount;
        }
    }

    private readonly Dictionary<IntPtr, Rack> racks = new();
    private readonly Settings settings;
    private readonly RackMenu menu;
    private bool pending;
    private bool applying;
    public RackStorage(Settings settings)
    {
        this.settings = settings;
        menu = new RackMenu(settings);
        settings.RackChanged += Request;
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
        string itemId = Normalize(definition?.ID);
        bool match = definition != null && (itemId == "smallstoragerack" || itemId == "mediumstoragerack" || itemId == "largestoragerack" ||
            itemId == "smallstoragecloset" || itemId == "mediumstoragecloset" || itemId == "largestoragecloset" || itemId == "hugestoragecloset" ||
            itemId == "safe" || itemId == "filingcabinet");
        if (!match) return null;
        var rack = new Rack(entity, itemId);
        racks.Add(entity.Pointer, rack);
        settings.Trace($"ESB_RACK_FOUND | id={definition?.ID} | name={entity.StorageEntityName} | originalSlots={rack.OriginalSlots} | originalRows={rack.OriginalRows}");
        return rack;
    }

    private int ConfiguredSlots(Rack rack) => settings.HostStorageValue(rack.ItemId, false, LocalSlots(rack));
    private int LocalSlots(Rack rack) => rack.ItemId switch
    {
        "smallstoragerack" => settings.SmallRackSlots.Value,
        "mediumstoragerack" => settings.MediumRackSlots.Value,
        "largestoragerack" => settings.LargeRackSlots.Value,
        "smallstoragecloset" => settings.SmallClosetSlots.Value,
        "mediumstoragecloset" => settings.MediumClosetSlots.Value,
        "largestoragecloset" => settings.LargeClosetSlots.Value,
        "hugestoragecloset" => settings.HugeClosetSlots.Value,
        "safe" => settings.SafeSlots.Value,
        "filingcabinet" => settings.FilingCabinetSlots.Value,
        _ => throw new InvalidOperationException("Unsupported storage identity.")
    };

    private int ConfiguredRows(Rack rack) => settings.HostStorageValue(rack.ItemId, true, LocalRows(rack));
    private int LocalRows(Rack rack) => rack.ItemId switch
    {
        "smallstoragerack" => settings.SmallRackRows.Value,
        "mediumstoragerack" => settings.MediumRackRows.Value,
        "largestoragerack" => settings.LargeRackRows.Value,
        "smallstoragecloset" => settings.SmallClosetRows.Value,
        "mediumstoragecloset" => settings.MediumClosetRows.Value,
        "largestoragecloset" => settings.LargeClosetRows.Value,
        "hugestoragecloset" => settings.HugeClosetRows.Value,
        "safe" => settings.SafeRows.Value,
        "filingcabinet" => settings.FilingCabinetRows.Value,
        _ => throw new InvalidOperationException("Unsupported storage identity.")
    };

    private static string Normalize(string? value)
        => new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public void Started(StorageEntity entity) { if (Track(entity) != null) Request(); }
    public void Removed(StorageEntity entity) => racks.Remove(entity.Pointer);

    public void ContentsChanged(StorageEntity entity)
    {
        if (applying || !racks.TryGetValue(entity.Pointer, out var rack) || rack.Failed) return;
        if (entity.ItemSlots.Count != StorageRules.TargetSlots(ConfiguredSlots(rack), rack.OriginalSlots)) Request();
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

    private static bool CanReorder(ItemSlot slot, HashSet<IntPtr> localGroups) =>
        !slot.IsLocked && !slot.IsRemovalLocked && !slot.IsAddLocked &&
        (slot.SiblingSet == null || localGroups.Contains(slot.SiblingSet.Pointer)) && (slot.PlayerFilter == null || slot.PlayerFilter.IsDefault());

    private static HashSet<IntPtr> LocalSiblingGroups(StorageEntity entity)
    {
        var slots = entity.ItemSlots;
        var owned = new HashSet<IntPtr>();
        foreach (var slot in slots) owned.Add(slot.Pointer);
        var local = new HashSet<IntPtr>();
        var inspected = new HashSet<IntPtr>();
        foreach (var slot in slots)
        {
            var group = slot.SiblingSet;
            if (group == null || !inspected.Add(group.Pointer)) continue;
            var members = group.Slots;
            // A separate membership list is required for safe removal.
            if (members == null || members.Pointer == slots.Pointer || members.Count == 0) continue;
            bool valid = true;
            var seen = new HashSet<IntPtr>();
            foreach (var member in members)
            {
                if (member == null || !owned.Contains(member.Pointer) || !seen.Add(member.Pointer) ||
                    member.SiblingSet == null || member.SiblingSet.Pointer != group.Pointer)
                { valid = false; break; }
            }
            // Also reject inconsistent back-references missing from membership.
            if (valid)
                foreach (var candidate in slots)
                    if (candidate.SiblingSet != null && candidate.SiblingSet.Pointer == group.Pointer &&
                        !seen.Contains(candidate.Pointer)) { valid = false; break; }
            if (valid) local.Add(group.Pointer);
        }
        return local;
    }

    private static bool SameHardFilters(ItemSlot left, ItemSlot right)
    {
        var a = left.HardFilters;
        var b = right.HardFilters;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i]?.Pointer != b[i]?.Pointer) return false;
        return true;
    }

    private void SyncVisualSlots(Rack rack)
    {
        // Native StorageEntityVisualizer.Start registers only the slots present
        // at startup. Keep its separate presentation list aligned after resizing.
        var entity = rack.Entity;
        var placeable = entity.GetComponentInParent<PlaceableStorageEntity>();
        if (placeable == null) return;
        foreach (var visualizer in placeable.GetComponentsInChildren<StorageEntityVisualizer>(true))
        {
            if (visualizer.storageEntity == null || visualizer.storageEntity.Pointer != entity.Pointer ||
                visualizer.itemSlots == null) continue;
            var visualSlots = visualizer.itemSlots;
            var owned = new HashSet<IntPtr>();
            foreach (var slot in entity.ItemSlots) owned.Add(slot.Pointer);
            int added = 0, removed = 0;
            // Do not mutate the owner's list if a native version shares it.
            if (visualSlots.Pointer != entity.ItemSlots.Pointer)
            {
                for (int i = visualSlots.Count - 1; i >= 0; i--)
                    if (visualSlots[i] == null || !owned.Contains(visualSlots[i].Pointer))
                    { visualSlots.RemoveAt(i); removed++; }
                var registered = new HashSet<IntPtr>();
                foreach (var slot in visualSlots) registered.Add(slot.Pointer);
                foreach (var slot in entity.ItemSlots)
                    if (registered.Add(slot.Pointer))
                    {
                        // Use the native API so item-change events are registered too.
                        visualizer.AddSlot(slot, false);
                        added++;
                    }
            }
            if (added != 0 || removed != 0)
            {
                visualizer.QueueRefresh();
                settings.Trace($"ESB_RACK_VISUALS | id={rack.ItemId} | inventory={entity.ItemSlots.Count} | visualSlots={visualSlots.Count} | added={added} | removed={removed} | footprintCapacity={visualizer.totalFootprintCapacity}");
            }
        }
    }

    private void Apply(Rack rack)
    {
        var entity = rack.Entity;
        if (entity == null || rack.Failed || entity.ItemSlots == null || entity.ItemSlots.Count == 0) return;
        var slots = entity.ItemSlots;
        int before = slots.Count;
        int target = StorageRules.TargetSlots(ConfiguredSlots(rack), rack.OriginalSlots);
        var localGroups = LocalSiblingGroups(entity);
        applying = true;
        try
        {
            if (before > target)
            {
                if (menu.IsShowing(entity)) menu.ClearBindings();
                // Reorder existing slots within the same owner. Item instances,
                // quantities and their event subscriptions remain untouched.
                for (int sourceIndex = target; sourceIndex < slots.Count; sourceIndex++)
                {
                    var source = slots[sourceIndex];
                    if (source.ItemInstance == null || !CanReorder(source, localGroups)) continue;
                    for (int destinationIndex = 0; destinationIndex < target; destinationIndex++)
                    {
                        var destination = slots[destinationIndex];
                        if (destination.ItemInstance != null || !CanReorder(destination, localGroups) ||
                            !SameHardFilters(source, destination)) continue;
                        slots[destinationIndex] = source;
                        slots[sourceIndex] = destination;
                        entity.ContentsChanged();
                        settings.Trace($"ESB_RACK_COMPACT | id={rack.ItemId} | from={sourceIndex + 1} | to={destinationIndex + 1}");
                        break;
                    }
                }
            }
            int size = StorageRules.SafeSize(target, slots.Count, i =>
                slots[i].ItemInstance != null || !CanReorder(slots[i], localGroups));
            // Clear displayed bindings before releasing an empty tail of slots.
            if (size < before && menu.IsShowing(entity)) menu.ClearBindings();
            Grow(entity, size);
            Il2CppSystem.Action contentsChanged = (Il2CppSystem.Action)entity.ContentsChanged;
            for (int i = slots.Count - 1; i >= size; i--)
            {
                var slot = slots[i];
                slot.onItemDataChanged -= contentsChanged;
                var group = slot.SiblingSet;
                if (group != null)
                {
                    if (!localGroups.Contains(group.Pointer))
                        throw new InvalidOperationException("Refusing to detach an external sibling group.");
                    for (int memberIndex = group.Slots.Count - 1; memberIndex >= 0; memberIndex--)
                        if (group.Slots[memberIndex].Pointer == slot.Pointer) group.Slots.RemoveAt(memberIndex);
                    slot._SiblingSet_k__BackingField = null!;
                }
                slots.RemoveAt(i);
                // SetSlotOwner dereferences its owner argument; null is not supported.
                slot._SlotOwner_k__BackingField = null!;
            }
            entity.SlotCount = slots.Count;
            if (before != slots.Count) entity.ContentsChanged();
            int rows = StorageRules.Rows(ConfiguredRows(rack), rack.OriginalRows, target);
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
            if (changed) settings.Trace($"ESB_RACK_APPLIED | id={rack.ItemId} | requested={target} | actual={slots.Count} | rows={rows}");
            if (size > target && rack.LastBlockedTarget != target)
            {
                settings.Trace($"ESB_RACK_SHRINK_DEFERRED | id={rack.ItemId} | requested={target} | protectedSize={size}");
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (i >= target || !CanReorder(slot, localGroups))
                        settings.Trace($"ESB_RACK_SLOT_GUARD | id={rack.ItemId} | slot={i + 1} | item={slot.ItemInstance != null} | locked={slot.IsLocked} | removalLocked={slot.IsRemovalLocked} | addLocked={slot.IsAddLocked} | playerFilter={slot.PlayerFilter != null && !slot.PlayerFilter.IsDefault()} | siblingSet={slot.SiblingSet != null} | siblings={slot.SiblingSet?.Slots.Count ?? 0} | hardFilters={slot.HardFilters.Count}");
                }
            }
            rack.LastBlockedTarget = size > target ? target : -1;
            // A presentation failure must not disable inventory resizing.
            try { SyncVisualSlots(rack); }
            catch (Exception ex) { settings.Error("ESB_RACK_VISUALS", ex); }
            if (menu.IsShowing(entity)) menu.Bind(entity, true);
        }
        finally { applying = false; }
    }

    public void Tick()
    {
        try { menu.LayoutTick(); }
        catch (Exception ex) { settings.Error("ESB_STORAGE_LAYOUT", ex); }
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
        settings.Trace($"ESB_STORAGE_OPEN | name={entity.StorageEntityName} | supportedRack={rack != null} | slots={entity.ItemSlots.Count}");
        if (rack == null || rack.Failed) return;
        if (!SaveOrLoadInProgress && !Dragging) Apply(rack);
        else Request();
        menu.Prepare(entity.ItemSlots.Count);
    }

    public void AfterOpen(StorageEntity entity)
    {
        if (racks.TryGetValue(entity.Pointer, out var rack) && !rack.Failed) menu.Bind(entity, true);
    }

    public void Closed() { menu.Restore(); Request(); }

    public void Dispose()
    {
        settings.RackChanged -= Request;
        settings.LanguageChanged -= Request;
        menu.Restore();
        menu.Dispose();
        racks.Clear();
    }
}
