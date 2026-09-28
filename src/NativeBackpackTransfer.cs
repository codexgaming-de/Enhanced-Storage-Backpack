using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Money;

namespace EnhancedStorageBackpack;

// Prepared on the host after identity/session/revision validation. The caller
// must hold an inventory lease and save barrier through Apply and acknowledgement.
// This adapter is deliberately not exposed as a network handler on its own.
internal sealed class NativeBackpackTransfer
{
    private readonly ItemSlot source, target;
    private readonly ItemInstance? oldSource, oldTarget, nextSource, nextTarget;
    private readonly string? sourceJson, targetJson, nextSourceJson, nextTargetJson;
    private bool used;
    internal BackpackTransferRules.Plan Plan { get; }
    private static string? Json(ItemSlot slot) => slot.ItemInstance?.GetItemData().GetJson(false);
    private static BackpackTransferRules.Slot Describe(ItemSlot slot) => new(
        slot.Quantity, slot.ItemInstance != null, slot.IsLocked, slot.IsAddLocked, slot.IsRemovalLocked);
    private static bool Unsupported(ItemSlot slot) => slot.SiblingSet != null || slot.TryCast<CashSlot>() != null ||
        slot.ItemInstance?.TryCast<CashInstance>() != null;

    internal NativeBackpackTransfer(ItemSlot source, ItemSlot target, int amount)
    {
        this.source = source; this.target = target;
        sourceJson = Json(source); targetJson = Json(target);
        var item = source.ItemInstance;
        var destination = target.ItemInstance;
        bool unsupported = Unsupported(source) || Unsupported(target);
        bool targetAccepts = !unsupported && item != null && target.DoesItemMatchHardFilters(item) && target.DoesItemMatchPlayerFilters(item);
        bool sourceAccepts = !unsupported && destination != null && source.DoesItemMatchHardFilters(destination) && source.DoesItemMatchPlayerFilters(destination);
        bool stack = item != null && destination != null && destination.CanStackWith(item, false);
        int capacity = item == null || unsupported ? 0 : target.GetCapacityForItem(item, true);
        Plan = BackpackTransferRules.Decide(Describe(source), Describe(target), amount,
            source.Pointer == target.Pointer, targetAccepts, sourceAccepts, stack, capacity);
        if (Plan.Kind == BackpackTransferRules.Kind.Reject) return;
        // Copies preserve native item subclasses and their metadata. Allocate and
        // validate all objects before touching either authoritative slot.
        oldSource = item!.GetCopy(); oldTarget = destination?.GetCopy();
        if (Plan.Kind == BackpackTransferRules.Kind.Swap)
        { nextSource = destination!.GetCopy(); nextTarget = item.GetCopy(); }
        else
        {
            nextSource = source.Quantity == Plan.Amount ? null : item.GetCopy(source.Quantity - Plan.Amount);
            nextTarget = destination == null ? item.GetCopy(Plan.Amount)
                : destination.GetCopy(checked(target.Quantity + Plan.Amount));
        }
        if (oldSource == null || (destination != null && oldTarget == null) || nextTarget == null ||
            ((Plan.Kind == BackpackTransferRules.Kind.Swap || source.Quantity != Plan.Amount) && nextSource == null))
            throw new InvalidOperationException("Native item copy failed before transfer.");
        nextSourceJson = nextSource?.GetItemData().GetJson(false);
        nextTargetJson = nextTarget?.GetItemData().GetJson(false);
    }
    // This is the single native commit entry point for a validated host session.
    // The caller supplies arrays from the authenticated player's native owners.
    // Wire/menu activation remains gated until save/rejoin integration is complete.
    internal static HostBackpackState.Outcome Execute(HostBackpackState host, ulong sender,
        HostBackpackState.Move request, ItemSlot[] inventory, ItemSlot[] backpack)
    {
        // A legitimate vanilla change invalidates the previously offered revision.
        host.RefreshNative(sender, inventory.Select(Json).ToArray(), backpack.Select(Json).ToArray());
        return host.ApplyNative(sender, request, snapshot =>
        {
            var from = request.FromArea == HostBackpackState.Area.Inventory ? inventory : backpack;
            var to = request.ToArea == HostBackpackState.Area.Inventory ? inventory : backpack;
            var transfer = new NativeBackpackTransfer(from[request.FromSlot], to[request.ToSlot], request.Amount);
            if (transfer.Plan.Kind == BackpackTransferRules.Kind.Reject) return null;
            var nextInventory = snapshot.Inventory;
            var nextBackpack = snapshot.Backpack;
            var nextFrom = request.FromArea == HostBackpackState.Area.Inventory ? nextInventory : nextBackpack;
            var nextTo = request.ToArea == HostBackpackState.Area.Inventory ? nextInventory : nextBackpack;
            nextFrom[request.FromSlot] = transfer.nextSourceJson;
            nextTo[request.ToSlot] = transfer.nextTargetJson;
            return new HostBackpackState.NativeChange(nextInventory, nextBackpack, transfer.Apply);
        });
    }
    internal void Apply()
    {
        if (used || Plan.Kind == BackpackTransferRules.Kind.Reject)
            throw new InvalidOperationException("Transfer is rejected or already used.");
        // Reject intervening vanilla changes or new locks, even on the same frame.
        if (Json(source) != sourceJson || Json(target) != targetJson || source.IsLocked ||
            source.IsRemovalLocked || target.IsLocked || target.IsAddLocked ||
            Unsupported(source) || Unsupported(target) ||
            (Plan.Kind == BackpackTransferRules.Kind.Swap && (source.IsAddLocked || target.IsRemovalLocked)))
            throw new InvalidOperationException("Native slots changed before transfer.");
        if (nextTarget != null && (!target.DoesItemMatchHardFilters(nextTarget) || !target.DoesItemMatchPlayerFilters(nextTarget)))
            throw new InvalidOperationException("Destination filter changed before transfer.");
        if (Plan.Kind == BackpackTransferRules.Kind.Swap && nextSource != null &&
            (!source.DoesItemMatchHardFilters(nextSource) || !source.DoesItemMatchPlayerFilters(nextSource)))
            throw new InvalidOperationException("Source filter changed before swap.");
        if (Plan.Kind != BackpackTransferRules.Kind.Swap &&
            target.GetCapacityForItem(source.ItemInstance, true) < Plan.Amount)
            throw new InvalidOperationException("Destination capacity changed before transfer.");
        used = true;
        try
        {
            // Internal avoids recursively invoking owner RPCs. The integrating
            // protocol is responsible for replicating the completed pair.
            source.SetStoredItem(nextSource!, true);
            target.SetStoredItem(nextTarget!, true);
            // Native/mod callbacks must not silently produce a different result.
            if (Json(source) != nextSourceJson || Json(target) != nextTargetJson)
                throw new InvalidOperationException("Native transfer result differs from the prepared pair.");
        }
        catch (Exception original)
        {
            // Attempt both restorations even if the first invokes a failing callback.
            var failures = new List<Exception> { original };
            try { source.SetStoredItem(oldSource!, true); } catch (Exception ex) { failures.Add(ex); }
            try { target.SetStoredItem(oldTarget!, true); } catch (Exception ex) { failures.Add(ex); }
            throw new AggregateException("Transfer failed; callers must keep interaction blocked until resynchronized.", failures);
        }
    }
}
