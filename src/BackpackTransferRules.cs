namespace EnhancedStorageBackpack;

// Inputs come from host-owned slots and native item/filter checks, never from
// client claims. A request contains only source, destination and requested amount.
internal static class BackpackTransferRules
{
    internal enum Kind { Reject, Move, Merge, Swap }
    internal sealed record Slot(int Quantity, bool Occupied, bool Locked = false,
        bool AddLocked = false, bool RemoveLocked = false);
    internal sealed record Plan(Kind Kind, int Amount = 0);
    internal static Plan Decide(Slot source, Slot target, int requested, bool sameSlot,
        bool targetAccepts, bool sourceAccepts, bool canStack, int targetCapacity)
    {
        if (sameSlot || !source.Occupied || source.Quantity <= 0 || requested <= 0 ||
            requested > source.Quantity || source.Locked || source.RemoveLocked ||
            target.Locked || target.AddLocked || !targetAccepts) return new(Kind.Reject);
        if (!target.Occupied || canStack)
        {
            int amount = Math.Min(requested, Math.Max(0, targetCapacity));
            // Capacity is checked natively but retain an independent overflow guard.
            if (amount == 0 || (target.Occupied &&
                (target.Quantity <= 0 || target.Quantity > int.MaxValue - amount))) return new(Kind.Reject);
            return new(target.Occupied ? Kind.Merge : Kind.Move, amount);
        }
        if (requested == source.Quantity && target.Quantity > 0 && sourceAccepts &&
            !source.AddLocked && !target.RemoveLocked) return new(Kind.Swap, requested);
        return new(Kind.Reject);
    }
}
