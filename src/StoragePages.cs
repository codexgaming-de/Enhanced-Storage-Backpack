namespace EnhancedStorageBackpack;

internal static class StoragePages
{
    // Keep the configured row count where it fits; split taller layouts across pages.
    internal static int Capacity(int rows) => Math.Min(40, Math.Clamp(rows, 1, 5) * 10);
}
