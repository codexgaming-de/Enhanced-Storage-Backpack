using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;

namespace EnhancedStorageBackpack;

// Bounded read-only transport. Sending a snapshot never grants a mutation lease.
internal sealed class NativeInventorySnapshots
{
    internal const string Key = "codexgaming.esb.inventory-snapshot.v1";
    private sealed class Export
    {
        internal Player Player = null!;
        internal InventorySnapshotWire Request = null!;
        internal InventorySnapshotWire[] Chunks = Array.Empty<InventorySnapshotWire>();
        internal int Next;
        internal float Expires, RetryAfter;
    }
    private readonly Settings settings;
    private readonly Func<Player, string> read;
    private readonly Dictionary<IntPtr, Export> exports = new();
    private InventorySnapshotWire? request;
    private InventorySnapshotReceiver? receiver;
    private float next, deadline;
    private int attempts;
    private bool finished;

    internal NativeInventorySnapshots(Settings settings, Func<Player, string> read)
    { this.settings = settings; this.read = read; }
    internal void HostRequest(Player player, InventorySnapshotWire incoming)
    {
        if (incoming.Kind != "request") return;
        float now = Time.realtimeSinceStartup;
        if (exports.TryGetValue(player.Pointer, out var existing))
        {
            if (!existing.Request.Matches(incoming)) exports.Remove(player.Pointer);
            else
            {
                if (now >= existing.RetryAfter)
                { existing.Next = 0; existing.RetryAfter = now + 4; }
                return; // Retries use the SAME captured inventory, never a mixture.
            }
        }
        if (exports.Count >= 4) return; // At most 1,048,576 retained base64 characters across four exports.
        var chunks = InventorySnapshotWire.Split(incoming, read(player));
        exports.Add(player.Pointer, new Export { Player = player, Request = incoming,
            Chunks = chunks, Expires = now + 20, RetryAfter = now + 4 });
        settings.Trace($"ESB_SNAPSHOT_HOST | captured native inventory and saved backpack | chunks={chunks.Length} | readOnly=true");
    }
    internal void TickHost(Func<Player, InventorySnapshotWire, bool> authenticated)
    {
        if (exports.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        foreach (var entry in exports.ToArray())
        {
            var export = entry.Value;
            if (now >= export.Expires || export.Player == null || !authenticated(export.Player, export.Request))
            { exports.Remove(entry.Key); continue; }
            // Do not flood the native reliable channel with a large backpack.
            try
            {
                for (int n = 0; n < 2 && export.Next < export.Chunks.Length; n++)
                {
                    export.Player.ReceiveValue(export.Player.Owner, Key, export.Chunks[export.Next].Encode());
                    export.Next++;
                }
            }
            catch
            {
                exports.Remove(entry.Key);
                throw; // Other exports remain intact; the caller logs once.
            }
        }
    }
    internal void TickClient(InventoryChannelProbe proof)
    {
        var current = InventorySnapshotWire.Request(proof);
        if (request == null || !request.Matches(current))
        { ResetClient(); request = current; receiver = new InventorySnapshotReceiver(current); deadline = Time.realtimeSinceStartup + 20; }
        if (!finished && Time.realtimeSinceStartup >= deadline)
        {
            finished = true; receiver = null;
            settings.Trace("ESB_SNAPSHOT_TIMEOUT | partial snapshot discarded; client inventory remains blocked");
        }
        if (finished || attempts >= 3 || Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 5; attempts++;
        Player.Local.SendValue(Key, request.Encode(), false);
        if (attempts == 3) settings.Trace("ESB_SNAPSHOT_WAIT | final request sent; client interactions remain blocked");
    }
    internal void Receive(InventorySnapshotWire packet)
    {
        if (finished || receiver == null || request == null || !packet.Matches(request)) return;
        if (!receiver.Accept(packet)) return;
        finished = true;
        settings.Trace("ESB_SNAPSHOT_CLIENT | complete host snapshot validated | readOnly=true | clientInventory=blocked");
    }
    private void ResetClient()
    { request = null; receiver = null; next = deadline = 0; attempts = 0; finished = false; }
    internal void Reset() { exports.Clear(); ResetClient(); }
}
