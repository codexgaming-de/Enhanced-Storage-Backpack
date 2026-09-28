using EnhancedStorageBackpack;
using static EnhancedStorageBackpack.HostBackpackState;

int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
void Throws(Action action, string name) { bool threw = false; try { action(); } catch (InvalidOperationException) { threw = true; } Check(threw, name); }
const string item = "{\"DataType\":\"ItemData\",\"ID\":\"sample\",\"Quantity\":17}";
var host = new HostBackpackState();
var seed = new string?[] { item, null };
host.Register(1, seed, new string?[4]); host.Register(2, new string?[2], new string?[4]);
seed[0] = null;
Check(host.Read(1).Inventory[0] == item, "registration copies host data");
string lease = host.Connect(1);
var move = new Move(host.Session, lease, 1, 0, Area.Inventory, 0, Area.Backpack, 3);
Check(host.Apply(2, move) == Outcome.Denied, "another sender cannot use player lease");
Check(host.Apply(1, move with { Session = "old-session" }) == Outcome.Denied, "cross-session request");
Check(host.Apply(1, move with { Sequence = 2 }) == Outcome.Stale, "out of order sequence");
Check(host.Apply(1, move with { ToSlot = 4 }) == Outcome.Invalid, "bounds rejected");
Check(host.Apply(1, move with { FromArea = (Area)8 }) == Outcome.Invalid, "unknown area rejected");
Check(host.Apply(1, move with { Amount = 5 }) == Outcome.Invalid, "partial request cannot enter whole-stack path");
Check(host.Apply(1, move) == Outcome.Applied, "whole stack moved");
Check(host.Read(1).Inventory[0] == null && host.Read(1).Backpack[3] == item, "atomic pair");
Check(host.Apply(1, move) == Outcome.Duplicate, "repeated packet does not move again");
Check(host.Apply(1, move with { ToSlot = 0 }) == Outcome.Stale, "same sequence altered command");
var copy = host.Read(1); copy.Backpack[3] = null;
Check(host.Read(1).Backpack[3] == item, "read cannot mutate authority");
var beforeSave = host.BeginSave();
var back = new Move(host.Session, lease, 2, 1, Area.Backpack, 3, Area.Inventory, 0);
Check(host.Apply(1, back) == Outcome.Busy, "save freezes transfers");
Throws(() => host.ResizeBackpack(1, 1), "save freezes compaction");
Throws(() => host.Connect(1), "save freezes reconnect");
Throws(() => host.BeginSave(), "nested saves rejected");
Throws(() => host.EndSave("wrong"), "wrong save completion rejected");
Check(host.Apply(1, back) == Outcome.Busy, "wrong completion leaves barrier intact");
host.EndSave(beforeSave.Ticket);
Check(host.Apply(1, back) == Outcome.Applied, "transfers resume after save");
Check(beforeSave.Players.Single(x => x.Player == 1).Backpack[3] == item, "saved snapshot immutable after move");
var restored = new HostBackpackState();
foreach (var saved in beforeSave.Players) restored.Register(saved.Player, saved.Inventory, saved.Backpack);
Check(restored.Read(1).Inventory[0] == null && restored.Read(1).Backpack[3] == item, "unsaved moves discarded together on reload");
Check(restored.Apply(1, back) == Outcome.Denied, "old session packets rejected after reload");
host.Disconnect(1);
Check(host.Apply(1, back) == Outcome.Denied, "disconnected sender rejected");
var newLease = host.Connect(1);
Check(newLease != lease && host.Read(1).Inventory[0] == item, "reconnect preserves unsaved live state");
Check(host.Apply(1, back) == Outcome.Denied, "old connection lease rejected");
var current = new Move(host.Session, newLease, 1, 2, Area.Inventory, 0, Area.Backpack, 3);
Check(host.Apply(1, current) == Outcome.Applied, "reconnected owner can move");
host.ResizeBackpack(1, 1);
Check(host.Read(1).Backpack.Length == 1 && host.Read(1).Backpack[0] == item, "compaction keeps full item JSON");
Check(host.Apply(1, current with { Sequence = 2 }) == Outcome.Stale, "resize invalidates stale revision");
var full = new HostBackpackState();
full.Register(3, new string?[1], new string?[] { item, item, item });
full.ResizeBackpack(3, 1);
Check(full.Read(3).Backpack.Length == 3 && full.Read(3).Backpack.All(x => x == item), "full backpack cannot lose items on shrink");
// Many delayed/replayed packets conserve stack count across both containers.
var stress = new HostBackpackState(); stress.Register(4, new string?[] { item }, new string?[1]);
string stressLease = stress.Connect(4);
for (int n = 1; n <= 1000; n++)
{
    bool into = n % 2 == 1;
    var request = new Move(stress.Session, stressLease, n, n - 1,
        into ? Area.Inventory : Area.Backpack, 0, into ? Area.Backpack : Area.Inventory, 0);
    Check(stress.Apply(4, request) == Outcome.Applied, "alternating transfer");
    Check(stress.Apply(4, request) == Outcome.Duplicate, "duplicate alternating transfer");
    var state = stress.Read(4);
    Check(state.Inventory.Concat(state.Backpack).Count(x => x == item) == 1, "conservation");
}

// Prepared native commits: fake native slots expose reentrancy/failure ordering.
var nativeHost = new HostBackpackState();
nativeHost.Register(7, new string?[] { item, null }, new string?[2]);
string nativeLease = nativeHost.Connect(7);
var nativeRequest = new Move(nativeHost.Session, nativeLease, 1, 0, Area.Inventory, 0, Area.Backpack, 1, 5);
int commits = 0, prepares = 0;
string remainder = "remaining native stack", moved = "copied native partial stack";
NativeChange Prepare(Snapshot state)
{
    prepares++;
    Check(nativeHost.Apply(7, nativeRequest) == Outcome.Busy, "reentrant transfer blocked during preparation");
    Throws(() => nativeHost.BeginSave(), "save cannot observe prepared operation");
    Throws(() => nativeHost.Read(7), "reentrant read cannot bypass save barrier");
    state.Inventory[0] = remainder; state.Backpack[1] = moved;
    return new NativeChange(state.Inventory, state.Backpack, () =>
    {
        commits++;
        Throws(() => nativeHost.BeginSave(), "native callback cannot save half pair");
        Throws(() => nativeHost.Connect(7), "native callback cannot rotate lease");
        Throws(() => nativeHost.Disconnect(7), "native callback cannot disconnect authority");
        Throws(() => nativeHost.ResizeBackpack(7, 1), "native callback cannot compact slots");
        Throws(() => nativeHost.RefreshNative(7, new string?[2], new string?[2]), "native callback cannot replace authority");
    });
}
Check(nativeHost.ApplyNative(7, nativeRequest with { Amount = 0 }, Prepare) == Outcome.Invalid, "zero native amount rejected");
Check(nativeHost.ApplyNative(7, nativeRequest with { ToSlot = 2 }, Prepare) == Outcome.Invalid, "native destination bounds checked before adapter");
Check(nativeHost.ApplyNative(7, nativeRequest with { FromArea = (Area)9 }, Prepare) == Outcome.Invalid, "invalid native area rejected");
Check(nativeHost.ApplyNative(8, nativeRequest, Prepare) == Outcome.Denied, "unknown sender never reaches native adapter");
Check(prepares == 0, "invalid request cannot prepare native state");
Check(nativeHost.ApplyNative(7, nativeRequest, Prepare) == Outcome.Applied, "prepared native split committed");
Check(commits == 1 && prepares == 1, "single prepare and native commit");
Check(nativeHost.Read(7).Inventory[0] == remainder && nativeHost.Read(7).Backpack[1] == moved, "native pair committed together");
Check(nativeHost.ApplyNative(7, nativeRequest, Prepare) == Outcome.Duplicate, "native retry acknowledged without commit");
Check(commits == 1 && prepares == 1, "duplicate has no native side effects");
Check(nativeHost.ApplyNative(7, nativeRequest with { Amount = 4 }, Prepare) == Outcome.Stale, "changed amount is not a duplicate");
var next = nativeRequest with { Sequence = 2, Revision = 1 };
var frozen = nativeHost.BeginSave();
Check(nativeHost.ApplyNative(7, next, Prepare) == Outcome.Busy, "world save freezes native commits");
Throws(() => nativeHost.RefreshNative(7, new string?[2], new string?[2]), "save freezes vanilla reconciliation");
nativeHost.EndSave(frozen.Ticket);
var live = nativeHost.Read(7);
nativeHost.RefreshNative(7, live.Inventory, live.Backpack);
Check(nativeHost.Read(7).Revision == 1, "unchanged native hotbar keeps revision");
live.Inventory[1] = "vanilla pickup";
nativeHost.RefreshNative(7, live.Inventory, live.Backpack);
live.Inventory[1] = null;
Check(nativeHost.Read(7).Inventory[1] == "vanilla pickup", "native refresh clones trusted state");
Check(nativeHost.ApplyNative(7, next, Prepare) == Outcome.Stale, "vanilla pickup invalidates old command");
next = next with { Revision = 2 };
Check(nativeHost.ApplyNative(7, next, _ => null) == Outcome.Invalid, "native filter rejection has no commit");
Check(nativeHost.Read(7).Revision == 2, "rejection does not consume revision");
Throws(() => nativeHost.ApplyNative(7, next, _ => throw new InvalidOperationException("copy failed")), "preparation failure propagates");
Check(nativeHost.Read(7).Revision == 2, "preparation failure leaves state readable");
Throws(() => nativeHost.ApplyNative(7, next, x => new NativeChange(new string?[1], x.Backpack, () => commits++)), "adapter cannot resize slots");
Throws(() => nativeHost.ApplyNative(7, next, x =>
{
    x.Inventory[1] = "unexpected modification";
    return new NativeChange(x.Inventory, x.Backpack, () => commits++);
}), "adapter cannot change unrelated slot");
Check(commits == 1 && nativeHost.Read(7).Inventory[1] == "vanilla pickup", "invalid adapter result never commits");
Throws(() => nativeHost.ApplyNative(7, next, x => new NativeChange(x.Inventory, x.Backpack, () =>
{
    commits++;
    throw new InvalidOperationException("native callback failed after source removal");
})), "native commit failure propagates");
Check(commits == 2, "failing native commit ran exactly once");
Check(nativeHost.ApplyNative(7, next, Prepare) == Outcome.Denied, "faulted session cannot replay");
Throws(() => nativeHost.Read(7), "uncertain native pair cannot be serialized");
Throws(() => nativeHost.BeginSave(), "faulted native inventory blocks coupled save");
Throws(() => nativeHost.Connect(7), "reconnect cannot clear native fault");
Throws(() => nativeHost.ResizeBackpack(7, 1), "compaction cannot clear native fault");
Throws(() => nativeHost.RefreshNative(7, new string?[2], new string?[2]), "refresh cannot hide native fault");
Check(prepares == 1, "denied stale duplicate and faulted calls never invoke adapter");
Console.WriteLine($"Host backpack: {checks} assertions passed.");
