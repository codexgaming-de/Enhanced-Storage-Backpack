using EnhancedStorageBackpack;

int tests = 0;
void Equal(int expected, int actual, string scenario)
{
    tests++;
    if (expected != actual) throw new Exception($"{scenario}: expected {expected}, got {actual}");
}
Equal(12, StorageRules.TargetSlots(0, 12), "Original capacity");
Equal(128, StorageRules.TargetSlots(999, 12), "Upper bound");
Equal(1, StorageRules.TargetSlots(-1, 12), "Lower bound");
Equal(5, StorageRules.SafeSize(5, 20, _ => false), "Empty tail shrinks");
Equal(20, StorageRules.SafeSize(5, 20, i => i == 19), "Last occupied slot retained");
Equal(9, StorageRules.SafeSize(5, 20, i => i == 8), "Protected index retained");
Equal(5, StorageRules.SafeSize(5, 20, i => i == 4), "Items below target retained");
Equal(128, StorageRules.SafeSize(128, 20, _ => true), "Growth does not remove slots");
Equal(5, StorageRules.Rows(10, 3, 5), "Rows cannot exceed slots");
Equal(3, StorageRules.Rows(0, 3, 20), "Original rows");
Equal(1, StorageRules.Rows(-1, 3, 20), "At least one row");
// Check every target and single protected position within the supported capacity.
for (int requested = 1; requested <= 128; requested++)
for (int occupied = 0; occupied < 128; occupied++)
{
    int size = StorageRules.SafeSize(requested, 128, i => i == occupied);
    tests++;
    if (size < requested || size <= occupied || size > 128)
        throw new Exception($"Lost protected slot: target={requested}, index={occupied}, result={size}");
}
Console.WriteLine($"{tests} checks passed.");
