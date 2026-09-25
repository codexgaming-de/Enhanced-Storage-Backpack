namespace EnhancedStorageBackpack;

internal static class StorageRules
{
    public static int TargetSlots(int configured, int original) => Math.Clamp(configured == 0 ? original : configured, 1, 128);

    public static int SafeSize(int requested, int current, Func<int, bool> protectedSlot)
    {
        for (int i = current - 1; i >= requested; i--)
            if (protectedSlot(i)) return i + 1;
        return requested;
    }

    public static int Rows(int configured, int original, int count)
        => Math.Clamp(configured == 0 ? original : configured, 1, Math.Max(1, count));
}
