namespace EnhancedStorageBackpack;

// Host-owned transaction core with a prepared native commit boundary.
// All calls must be serialized on the host's game thread. Items are opaque game
// JSON: clients request slot operations, never submit replacement item payloads.
internal sealed class HostBackpackState
{
    internal enum Area { Inventory, Backpack }
    internal enum Outcome { Applied, Duplicate, Denied, Stale, Busy, Invalid }
    internal sealed record Move(string Session, string Lease, long Sequence, long Revision,
        Area FromArea, int FromSlot, Area ToArea, int ToSlot, int Amount = 0);
    internal sealed record Snapshot(ulong Player, long Revision, string?[] Inventory, string?[] Backpack);
    // Trusted host adapter only; never deserialize this from the network.
    internal sealed record NativeChange(string?[] Inventory, string?[] Backpack, Action Commit);
    private sealed class State
    {
        internal string Lease = "";
        internal long Sequence, Revision;
        internal Move? LastMove;
        internal bool Faulted;
        internal string?[] Inventory = Array.Empty<string?>(), Backpack = Array.Empty<string?>();
    }
    private readonly Dictionary<ulong, State> players = new();
    private string? saving;
    private bool applying;
    internal string Session { get; } = Guid.NewGuid().ToString("N");

    // Only trusted host loading/registration may call this; never a network DTO.
    internal void Register(ulong player, string?[] inventory, string?[] backpack)
    {
        if (saving != null || applying) throw new InvalidOperationException("Inventory operation in progress.");
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
        if (saving != null || applying) throw new InvalidOperationException("Inventory operation in progress.");
        var state = players[player];
        Healthy(state);
        state.Lease = Guid.NewGuid().ToString("N"); state.Sequence = 0; state.LastMove = null;
        return state.Lease;
    }
    internal void Disconnect(ulong player)
    {
        if (applying) throw new InvalidOperationException("Native commit in progress.");
        if (players.TryGetValue(player, out var state)) state.Lease = "";
    }
    internal Snapshot Read(ulong player)
    {
        if (applying) throw new InvalidOperationException("Native commit in progress.");
        var state = players[player];
        Healthy(state);
        return new Snapshot(player, state.Revision, (string?[])state.Inventory.Clone(), (string?[])state.Backpack.Clone());
    }
    private Outcome? CheckRequest(ulong authenticatedSender, Move request)
    {
        if (request == null || request.Session != Session ||
            !players.TryGetValue(authenticatedSender, out var state) ||
            state.Faulted || state.Lease.Length == 0 || request.Lease != state.Lease) return Outcome.Denied;
        if (request.Sequence == state.Sequence && request == state.LastMove) return Outcome.Duplicate;
        if (saving != null || applying) return Outcome.Busy;
        if (state.Sequence == long.MaxValue || state.Revision == long.MaxValue ||
            request.Sequence != state.Sequence + 1 || request.Revision != state.Revision) return Outcome.Stale;
        return null;
    }
    internal Outcome Apply(ulong authenticatedSender, Move request)
    {
        var rejected = CheckRequest(authenticatedSender, request);
        if (rejected != null) return rejected.Value;
        if (request.Amount != 0) return Outcome.Invalid; // Quantities require the native adapter.
        var state = players[authenticatedSender];
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
    private static void Healthy(State state)
    {
        if (state.Faulted) throw new InvalidOperationException("Native inventory fault requires a game reload.");
    }
    // Refresh from native host slots, never from a client's replacement JSON.
    // Native gameplay may change the hotbar between two backpack requests.
    internal void RefreshNative(ulong player, string?[] inventory, string?[] backpack)
    {
        if (saving != null || applying) throw new InvalidOperationException("Inventory operation in progress.");
        var state = players[player]; Healthy(state);
        Validate(inventory); Validate(backpack);
        if (state.Inventory.SequenceEqual(inventory) && state.Backpack.SequenceEqual(backpack)) return;
        if (state.Revision == long.MaxValue) throw new InvalidOperationException("Revision exhausted.");
        var nextInventory = (string?[])inventory.Clone();
        var nextBackpack = (string?[])backpack.Clone();
        state.Inventory = nextInventory; state.Backpack = nextBackpack; state.Revision++;
    }
    internal Outcome ApplyNative(ulong sender, Move request, Func<Snapshot, NativeChange?> prepare)
    {
        var rejected = CheckRequest(sender, request);
        if (rejected != null) return rejected.Value;
        var state = players[sender];
        var source = Slots(state, request.FromArea); var target = Slots(state, request.ToArea);
        if (request.Amount <= 0 || source == null || target == null ||
            request.FromSlot < 0 || request.FromSlot >= source.Length ||
            request.ToSlot < 0 || request.ToSlot >= target.Length ||
            (ReferenceEquals(source, target) && request.FromSlot == request.ToSlot)) return Outcome.Invalid;
        var before = Read(sender);
        applying = true;
        bool commitStarted = false;
        try
        {
            var change = prepare(before);
            if (change == null) return Outcome.Invalid;
            Validate(change.Inventory); Validate(change.Backpack);
            if (change.Commit == null || change.Inventory.Length != state.Inventory.Length ||
                change.Backpack.Length != state.Backpack.Length)
                throw new InvalidOperationException("Native transfer cannot resize inventories.");
            for (int i = 0; i < change.Inventory.Length; i++)
                if (!(request.FromArea == Area.Inventory && request.FromSlot == i) &&
                    !(request.ToArea == Area.Inventory && request.ToSlot == i) &&
                    change.Inventory[i] != state.Inventory[i])
                    throw new InvalidOperationException("Native adapter modified an unrelated inventory slot.");
            for (int i = 0; i < change.Backpack.Length; i++)
                if (!(request.FromArea == Area.Backpack && request.FromSlot == i) &&
                    !(request.ToArea == Area.Backpack && request.ToSlot == i) &&
                    change.Backpack[i] != state.Backpack[i])
                    throw new InvalidOperationException("Native adapter modified an unrelated backpack slot.");
            // All allocations and serialization must succeed BEFORE changing native slots.
            var nextInventory = (string?[])change.Inventory.Clone();
            var nextBackpack = (string?[])change.Backpack.Clone();
            commitStarted = true;
            change.Commit();
            state.Inventory = nextInventory; state.Backpack = nextBackpack;
            state.Revision++; state.Sequence++; state.LastMove = request;
            return Outcome.Applied;
        }
        catch
        {
            // A native callback may have run even if the adapter attempted rollback.
            // Do not reconnect, replay, read or save a state whose consistency is unknown.
            if (commitStarted) { state.Faulted = true; state.Lease = ""; }
            throw;
        }
        finally { applying = false; }
    }
    private static string?[]? Slots(State state, Area area) => area switch
    { Area.Inventory => state.Inventory, Area.Backpack => state.Backpack, _ => null };

    // Host-only compaction. Occupied slots beyond requested capacity are retained
    // when no free destination exists; resizing never throws items away.
    internal void ResizeBackpack(ulong player, int capacity)
    {
        if (saving != null || applying) throw new InvalidOperationException("Inventory operation in progress.");
        if (capacity < 1 || capacity > 128) throw new ArgumentOutOfRangeException(nameof(capacity));
        var state = players[player];
        Healthy(state);
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
        if (saving != null || applying) throw new InvalidOperationException("Inventory operation in progress.");
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
