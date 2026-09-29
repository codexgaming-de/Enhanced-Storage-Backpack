namespace EnhancedStorageBackpack;

internal static class CashTransferRules
{
    internal static float Amount(float available, float balance, float requested, bool wallet)
    {
        if (!float.IsFinite(available) || !float.IsFinite(balance) || !float.IsFinite(requested) ||
            available <= 0 || balance < 0 || requested <= 0 || requested > 1000) return 0;
        float moved = Math.Min(requested, Math.Min(available, (wallet ? float.MaxValue : 1000) - balance));
        if (moved <= 0 || !float.IsFinite(balance + moved) || balance + moved == balance || available - moved == available) return 0;
        // Reject amounts lost to float rounding rather than create or destroy
        // money at extreme wallet balances. Native cash is stored as a float.
        double removed = (double)available - (available - moved);
        double added = (double)(balance + moved) - balance;
        if (Math.Abs(removed - added) > 0.001 || Math.Abs(removed - moved) > 0.001) return 0;
        return moved;
    }
}
