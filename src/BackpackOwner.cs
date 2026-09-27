using Il2CppInterop.Runtime.Injection;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Object;
using Il2CppScheduleOne.ItemFramework;

namespace EnhancedStorageBackpack;

// Pure slot owner: no StorageEntity, network spawning, police inventory or world object.
public sealed class BackpackOwner : Il2CppSystem.Object
{
    public BackpackOwner(IntPtr pointer) : base(pointer) { }
    public BackpackOwner() : base(ClassInjector.DerivedConstructorPointer<BackpackOwner>())
        => ClassInjector.DerivedConstructorBody(this);

    public Il2CppSystem.Collections.Generic.List<ItemSlot> ItemSlots { get; set; } = new();
    public void SetStoredInstance(NetworkConnection conn, int itemSlotIndex, ItemInstance instance)
        => ItemSlots[itemSlotIndex].SetStoredItem(instance, true);
    public void SetItemSlotQuantity(int itemSlotIndex, int quantity)
        => ItemSlots[itemSlotIndex].SetQuantity(quantity, true);
    public void SetSlotLocked(NetworkConnection conn, int itemSlotIndex, bool locked, NetworkObject lockOwner, string lockReason)
    {
        if (locked) ItemSlots[itemSlotIndex].ApplyLock(lockOwner, lockReason, true);
        else ItemSlots[itemSlotIndex].RemoveLock(true);
    }
    public void SetSlotFilter(NetworkConnection conn, int itemSlotIndex, SlotFilter filter)
        => ItemSlots[itemSlotIndex].SetPlayerFilter(filter, true);
    public void SendItemSlotDataToClient(NetworkConnection conn) { } // Singleplayer only.
    public int GetQuantitySum()
    {
        int count = 0;
        foreach (var slot in ItemSlots) count += slot.Quantity;
        return count;
    }
    public int GetQuantityOfItem(string id)
    {
        int count = 0;
        foreach (var slot in ItemSlots)
            if (slot.ItemInstance?.Definition?.ID == id) count += slot.Quantity;
        return count;
    }
    public int GetNonEmptySlotCount()
    {
        int count = 0;
        foreach (var slot in ItemSlots) if (slot.ItemInstance != null) count++;
        return count;
    }
    public ItemSlot GetFirstSlotContaining(string id)
    {
        foreach (var slot in ItemSlots)
            if (slot.ItemInstance?.Definition?.ID == id) return slot;
        return null!;
    }
}
