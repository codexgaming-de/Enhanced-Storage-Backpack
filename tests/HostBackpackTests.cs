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
Console.WriteLine($"Host backpack: {checks} assertions passed.");
