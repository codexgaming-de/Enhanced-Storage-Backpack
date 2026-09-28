namespace EnhancedStorageBackpack;

// Host-owned transaction core. Not connected to native inventories yet.
// All calls must be serialized on the host's game thread. Items are opaque game
// JSON: clients request slot operations, never submit replacement item payloads.
internal sealed class HostBackpackState
{
    internal enum Area { Inventory, Backpack }
    internal enum Outcome { Applied, Duplicate, Denied, Stale, Busy, Invalid }
    internal sealed record Move(string Session, string Lease, long Sequence, long Revision,
        Area FromArea, int FromSlot, Area ToArea, int ToSlot);
    internal sealed record Snapshot(ulong Player, long Revision, string?[] Inventory, string?[] Backpack);
    private sealed class State
    {
        internal string Lease = "";
        internal long Sequence, Revision;
        internal Move? LastMove;
        internal string?[] Inventory = Array.Empty<string?>(), Backpack = Array.Empty<string?>();
    }
    private readonly Dictionary<ulong, State> players = new();
    private string? saving;
    internal string Session { get; } = Guid.NewGuid().ToString("N");

    // Only trusted host loading/registration may call this; never a network DTO.
    internal void Register(ulong player, string?[] inventory, string?[] backpack)
    {
        if (saving != null) throw new InvalidOperationException("Save in progress.");
        if (player == 0 || players.ContainsKey(player) || players.Count >= 64)
            throw new InvalidOperationException("Invalid or duplicate player registration.");
        Validate(inventory); Validate(backpack);
        players.Add(player, new State { Inventory = (string?[])inventory.Clone(), Backpack = (string?[])backpack.Clone() });
    }
    private static void Validate(string?[] items)
    {
        if (items == null || items.Length < 1 || items.Length > 128 ||
            items.Any(x => x != null && (x.Length == 0 || x.Length > 65536)))
            throw new ArgumentException("Invalid host inventory snapshot.");
    }
    // Reconnect rotates the lease but retains unsaved host state; loading disk on
    // reconnect would restore items already moved to somebody else's storage.
    internal string Connect(ulong player)
    {
        if (saving != null) throw new InvalidOperationException("Save in progress.");
        var state = players[player];
        state.Lease = Guid.NewGuid().ToString("N"); state.Sequence = 0; state.LastMove = null;
        return state.Lease;
    }
    internal void Disconnect(ulong player)
    {
        if (players.TryGetValue(player, out var state)) state.Lease = "";
    }
    internal Snapshot Read(ulong player)
    {
        var state = players[player];
        return new Snapshot(player, state.Revision, (string?[])state.Inventory.Clone(), (string?[])state.Backpack.Clone());
    }
    internal Outcome Apply(ulong authenticatedSender, Move request)
    {
        if (request == null || request.Session != Session ||
            !players.TryGetValue(authenticatedSender, out var state) ||
            state.Lease.Length == 0 || request.Lease != state.Lease) return Outcome.Denied;
        if (request.Sequence == state.Sequence && request == state.LastMove) return Outcome.Duplicate;
        if (saving != null) return Outcome.Busy;
        if (state.Sequence == long.MaxValue || state.Revision == long.MaxValue ||
            request.Sequence != state.Sequence + 1 || request.Revision != state.Revision) return Outcome.Stale;
        var source = Slots(state, request.FromArea);
        var target = Slots(state, request.ToArea);
        if (source == null || target == null || request.FromSlot < 0 || request.FromSlot >= source.Length ||
            request.ToSlot < 0 || request.ToSlot >= target.Length ||
            (ReferenceEquals(source, target) && request.FromSlot == request.ToSlot) ||
            source[request.FromSlot] == null || target[request.ToSlot] != null) return Outcome.Invalid;
        // No callbacks or awaits between removal and insertion. Whole-stack only;
        // split/merge needs game-specific quantity and compatibility validation.
        target[request.ToSlot] = source[request.FromSlot]; source[request.FromSlot] = null;
        state.Revision++; state.Sequence++; state.LastMove = request;
        return Outcome.Applied;
    }
    private static string?[]? Slots(State state, Area area) => area switch
    { Area.Inventory => state.Inventory, Area.Backpack => state.Backpack, _ => null };

    // Host-only compaction. Occupied slots beyond requested capacity are retained
    // when no free destination exists; resizing never throws items away.
    internal void ResizeBackpack(ulong player, int capacity)
    {
        if (saving != null) throw new InvalidOperationException("Save in progress.");
        if (capacity < 1 || capacity > 128) throw new ArgumentOutOfRangeException(nameof(capacity));
        var state = players[player];
        if (state.Backpack.Length == capacity) return;
        if (state.Revision == long.MaxValue) throw new InvalidOperationException("Revision exhausted.");
        var slots = new string?[Math.Max(capacity, state.Backpack.Length)];
        Array.Copy(state.Backpack, slots, state.Backpack.Length);
        for (int i = capacity; i < slots.Length; i++)
            if (slots[i] != null)
                for (int j = 0; j < capacity; j++)
                    if (slots[j] == null) { slots[j] = slots[i]; slots[i] = null; break; }
        int size = capacity;
        for (int i = capacity; i < slots.Length; i++) if (slots[i] != null) size = i + 1;
        Array.Resize(ref slots, size);
        if (state.Backpack.SequenceEqual(slots)) return;
        state.Backpack = slots; state.Revision++;
    }
    internal (string Ticket, Snapshot[] Players) BeginSave()
    {
        if (saving != null) throw new InvalidOperationException("Save already in progress.");
        // Build before freezing so allocation failures cannot leave a stuck barrier.
        var snapshots = players.Keys.Select(Read).ToArray();
        saving = Guid.NewGuid().ToString("N");
        return (saving, snapshots);
    }
    internal void EndSave(string ticket)
    {
        if (saving == null || ticket != saving) throw new InvalidOperationException("Wrong save ticket.");
        saving = null;
    }
}
