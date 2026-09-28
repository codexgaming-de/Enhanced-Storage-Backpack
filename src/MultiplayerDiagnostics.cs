using Il2CppFishNet;
using Il2CppScheduleOne.PlayerScripts;

namespace EnhancedStorageBackpack;

// First integration milestone. No client inventory interaction is enabled here.
// No remote inventory is cached or saved, and no new file is created.
internal sealed class MultiplayerDiagnostics
{
    private readonly Settings settings;
    private string session = Guid.NewGuid().ToString("N");
    private readonly Dictionary<IntPtr, int> labels = new();
    private readonly HashSet<string> seen = new();
    private int nextLabel;

    internal MultiplayerDiagnostics(Settings settings) => this.settings = settings;

    internal void Reset()
    {
        session = Guid.NewGuid().ToString("N");
        labels.Clear();
        seen.Clear();
        nextLabel = 0;
    }

    internal MultiplayerProtocol.Offer HostOffer() => new()
    {
        Protocol = MultiplayerProtocol.Version,
        Build = MultiplayerProtocol.Build,
        Session = session,
        BackpackSlots = settings.BackpackSlots.Value,
        Slots = new[] { settings.SmallRackSlots.Value, settings.MediumRackSlots.Value,
            settings.LargeRackSlots.Value, settings.SmallClosetSlots.Value,
            settings.MediumClosetSlots.Value, settings.LargeClosetSlots.Value,
            settings.HugeClosetSlots.Value, settings.SafeSlots.Value, settings.FilingCabinetSlots.Value },
        Rows = new[] { settings.SmallRackRows.Value, settings.MediumRackRows.Value,
            settings.LargeRackRows.Value, settings.SmallClosetRows.Value,
            settings.MediumClosetRows.Value, settings.LargeClosetRows.Value,
            settings.HugeClosetRows.Value, settings.SafeRows.Value, settings.FilingCabinetRows.Value }
    };

    internal void Incoming(Player player, ref string inventory)
    {
        // Called only at the native receive logic, not on arbitrary player input.
        try
        {
            var offer = MultiplayerProtocol.Extract(ref inventory);
            Observe(offer == null ? "host-offer-absent" : "host-offer-compatible", player, inventory.Length);
            // Receiving a valid offer does NOT unlock anything in this milestone.
        }
        catch (Exception ex) { Report("host-offer-rejected", ex); }
    }

    internal void Observe(string stage, Player? player, int characters = -1)
    {
        if (!settings.DebugLogging.Value) return;
        try
        {
            int label = 0;
            bool local = false;
            if (player != null)
            {
                local = player.IsLocalPlayer || (Player.Local != null && Player.Local.Pointer == player.Pointer);
                if (!labels.TryGetValue(player.Pointer, out label))
                {
                    if (labels.Count >= 64) return;
                    labels.Add(player.Pointer, label = ++nextLabel);
                }
            }
            string role = InstanceFinder.IsClientOnly ? "client" : InstanceFinder.IsServer ? "host" : "offline";
            string key = $"{stage}:{role}:{label}";
            if (!seen.Add(key)) return;
            // No player names, Steam IDs, save paths, item payloads or session tokens.
            settings.Trace($"ESB_MP_PATH | stage={stage} | role={role} | peer={label} | local={local} | chars={characters} | clientInventory=blocked");
        }
        catch { /* Observation must never interrupt native save/load. */ }
    }

    private void Report(string stage, Exception error)
    {
        try
        {
            if (seen.Add("error:" + stage)) settings.Error("ESB_MP_" + stage, error);
        }
        catch { /* Preserve native execution even if logging fails. */ }
    }
}
