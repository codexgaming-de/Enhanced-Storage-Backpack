using EnhancedStorageBackpack;
using static EnhancedStorageBackpack.BackpackTransferRules;
int checks = 0;
void Check(Plan plan, Kind kind, int amount, string name)
{ checks++; if (plan.Kind != kind || plan.Amount != amount) throw new Exception(name); }
var source = new Slot(20, true);
var empty = new Slot(0, false);
var other = new Slot(8, true);
Plan Move(Slot? s = null, Slot? t = null, int qty = 20, bool same = false,
    bool accepts = true, bool reverse = true, bool stack = false, int capacity = 20)
    => Decide(s ?? source, t ?? empty, qty, same, accepts, reverse, stack, capacity);
Check(Move(), Kind.Move, 20, "whole stack");
Check(Move(qty: 1), Kind.Move, 1, "right click single item");
Check(Move(qty: 7), Kind.Move, 7, "partial stack");
Check(Move(capacity: 5), Kind.Move, 5, "destination capacity caps transfer");
Check(Move(t: other, stack: true, capacity: 2), Kind.Merge, 2, "partial merge");
Check(Move(t: other, stack: true, capacity: 0), Kind.Reject, 0, "full target");
Check(Move(t: other), Kind.Swap, 20, "whole incompatible stack swap");
Check(Move(t: other, qty: 10), Kind.Reject, 0, "partial incompatible swap");
Check(Move(qty: 0), Kind.Reject, 0, "zero amount");
Check(Move(qty: -1), Kind.Reject, 0, "negative amount");
Check(Move(qty: 21), Kind.Reject, 0, "amount exceeds owned quantity");
Check(Move(qty: int.MaxValue), Kind.Reject, 0, "oversized request");
Check(Move(s: empty), Kind.Reject, 0, "empty source");
Check(Move(same: true), Kind.Reject, 0, "source equals target");
Check(Move(s: source with { Locked = true }), Kind.Reject, 0, "source lock");
Check(Move(s: source with { RemoveLocked = true }), Kind.Reject, 0, "source removal lock");
Check(Move(t: empty with { Locked = true }), Kind.Reject, 0, "target lock");
Check(Move(t: empty with { AddLocked = true }), Kind.Reject, 0, "target addition lock");
Check(Move(accepts: false), Kind.Reject, 0, "destination filter");
Check(Move(t: other, reverse: false), Kind.Reject, 0, "source filter on swap");
Check(Move(s: source with { AddLocked = true }, t: other), Kind.Reject, 0, "swap respects source add lock");
Check(Move(t: other with { RemoveLocked = true }), Kind.Reject, 0, "swap respects target removal lock");
Check(Move(s: source with { AddLocked = true }), Kind.Move, 20, "source add lock allows outgoing move");
Check(Move(t: other with { RemoveLocked = true }, stack: true), Kind.Merge, 20, "target removal lock allows incoming merge");
Check(Move(t: new Slot(int.MaxValue, true), stack: true), Kind.Reject, 0, "integer overflow rejected");
Check(Move(capacity: -1), Kind.Reject, 0, "negative native capacity");
Console.WriteLine($"Backpack transfer rules: {checks} scenarios passed.");
