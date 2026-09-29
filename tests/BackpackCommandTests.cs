using EnhancedStorageBackpack;

int checks = 0;
void Check(bool value) { checks++; if (!value) throw new Exception("Failed check " + checks); }
void Reject(Action action) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected); }
BackpackCommand Command() => new() { Session = Guid.NewGuid().ToString("N"), Token = Guid.NewGuid().ToString("N"),
    Nonce = Guid.NewGuid().ToString("N"), Lease = Guid.NewGuid().ToString("N"), Kind = "move", From = 0,
    ToBackpack = true, To = 127, Amount = 10 };
var command = Command();
Check(BackpackCommand.Decode(command.Encode()).To == 127);
command.To = 128; Reject(() => command.Encode());
command = Command(); command.From = 9; Reject(() => command.Encode());
command = Command(); command.Amount = 0; Reject(() => command.Encode());
command = Command(); command.Revision = -1; Reject(() => command.Encode());
command = Command(); command.ToBackpack = false; command.To = 1; Reject(() => command.Encode());
command = Command(); command.Lease = "bad"; Reject(() => command.Encode());
command = Command(); command.CashAmount = float.NaN; Reject(() => command.Encode());
command = Command(); command.CashAmount = 1001; Reject(() => command.Encode());
command = Command(); command.Build = "old"; Reject(() => command.Encode());
Reject(() => BackpackCommand.Decode(new string('x', 4097)));
foreach (string kind in new[] { "open", "commit", "abort", "freeze", "frozen", "release", "layout", "recovered", "resync" })
{ command = Command(); command.Kind = kind; Check(BackpackCommand.Decode(command.Encode()).Kind == kind); }
var before = new string?[9]; before[0] = "A"; before[1] = "B";
var after = (string?[])before.Clone(); after[0] = "B"; after[1] = "A";
var receipts = new NativeMoveReceipts(before, after);
Check(!receipts.Complete);
Check(!receipts.Accept(0, "A")); Check(!receipts.Complete);
Check(!receipts.Accept(5, null));
Check(receipts.Accept(1, "A")); Check(!receipts.Complete);
Check(receipts.Accept(1, "A")); Check(!receipts.Complete);
Check(receipts.Accept(0, "B")); Check(receipts.Complete);
Check(receipts.Indices.SequenceEqual(new[] { 0, 1 }));
Check(new NativeMoveReceipts(before, before).Complete);
after = (string?[])before.Clone(); after[8] = "cash";
Check(new NativeMoveReceipts(before, after).Indices.SequenceEqual(new[] { 8 }));
Reject(() => new NativeMoveReceipts(new string?[8], new string?[8]));
Check(CashTransferRules.Amount(500, 0, 1000, false) == 500);
Check(CashTransferRules.Amount(500, 950, 500, false) == 50);
Check(CashTransferRules.Amount(500, 1000, 500, false) == 0);
Check(CashTransferRules.Amount(500, 1000, 500, true) == 500);
Check(CashTransferRules.Amount(500, float.MaxValue, 100, true) == 0);
Check(CashTransferRules.Amount(float.MaxValue, 0, 100, true) == 0);
Check(CashTransferRules.Amount(500, 0, float.NaN, false) == 0);
Check(CashTransferRules.Amount(-1, 0, 100, false) == 0);
Check(CashTransferRules.Amount(500, -1, 100, false) == 0);
Check(CashTransferRules.Amount(500, 0, 1001, false) == 0);
Check(CashTransferRules.Amount(0.5f, 0, 1, false) == 0.5f);
// A two-slot swap cannot enter the host core after just one native receipt.
var host = new HostBackpackState();
var inv = new string?[9]; inv[0] = "hotbar";
host.Register(1, inv, new string?[] { "backpack" });
string lease = host.Connect(1);
var move = new HostBackpackState.Move(host.Session, lease, 1, 0,
    HostBackpackState.Area.Inventory, 0, HostBackpackState.Area.Backpack, 0, 1);
var next = (string?[])inv.Clone(); next[0] = "backpack";
var gate = new NativeMoveReceipts(inv, next);
Check(!gate.Complete); Check(host.Read(1).Inventory[0] == "hotbar");
Check(gate.Accept(0, "backpack"));
int commits = 0;
Check(host.ApplyNative(1, move, _ => new(next, new string?[] { "hotbar" }, () => commits++)) == HostBackpackState.Outcome.Applied);
Check(host.ApplyNative(1, move, _ => throw new Exception("replayed")) == HostBackpackState.Outcome.Duplicate);
Check(commits == 1 && host.Read(1).Inventory[0] == "backpack" && host.Read(1).Backpack[0] == "hotbar");
Console.WriteLine($"Backpack command/receipt/cash checks passed: {checks}");
