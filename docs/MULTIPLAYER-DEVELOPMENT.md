# Multiplayer implementation — 0.2.0-beta.1

Branch: `feature/0.2.0-multiplayer-beta`. Stable `main` remains 0.1.6.
Date: 2026-09-28. Author/project lead: CoDeX-Gaming; Codex-assisted implementation.

## Runtime connections

`SteamHostSettings` authenticates Steam lobby membership, exact build, session and
client token. Capacities come from the host without overwriting local preferences.
Transport identity survives configuration re-acknowledgement, so an in-flight
inventory transfer is not discarded by a settings update.

`NativeInventoryChannel` authenticates the real FishNet reader connection against
the player owner. Reserved native SendValue/ReceiveValue keys carry intent and
bounded snapshots on the same reliable channel as vanilla item replication.
Item mutations do not use the independently ordered Steam settings channel.

`RemoteBackpackRuntime` handles native menu clicks/drag cleanup, host plans,
owner hotbar mirrors, ordered native receipts, host commits, replay replies,
abort recovery and freeze/release barriers. `HostBackpackState` remains the
revision/lease/commit boundary. `NativeBackpackTransfer` prepares copies and
validates the pair before native writes. On commit failure the session is blocked;
rollback attempts are not represented as proof that arbitrary callbacks were undone.

`NativeMoveReceipts` waits for every changed native hotbar slot before commit.
During abort recovery, late native receipts for affected slots are suppressed
until the client confirms recovery. Duplicate command requests resend a cached
reply. An acknowledgement does not perform a second hotbar replacement.

`RemoteBackpackSaves` now accepts trusted live host backpack updates. The existing
`NativeRemotePlayerJournal` couples current native inventory with this payload,
retains offline player state for the next world save, and provides live rejoin
data. Moves and disconnects do not write independent backpack files.

`NativeStorageSync` grows supported client arrays before indexed RPC application,
applies host layouts, sends all fields including empty/unlocked/default slots,
and streams replication at eight queued actions per frame without materializing
an unbounded per-slot action queue. A layout arriving before its local object
requests another synchronization. Custom storage cannot open before layout admission.

World saves and storage reconfiguration drain client input through ordered freeze
acknowledgements. A pending client command during the freeze is answered without
mutation, allowing it to finish and acknowledge the barrier. Missing/incompatible
clients or a fault cannot be silently counted as a successful save.

## Validation boundary

- Full source compiles against supplied IL2CPP/MelonLoader/SteamNetworkLib refs
  using Roslyn and the available .NET 8 reference pack: zero errors, 14 CS1701
  reference-version warnings. This output is **not** a distributable net6 build.
- Standalone protocol, host-state, transfer, snapshot, journal and new command/
  receipt/cash tests pass. The journal suite exercises isolated temporary files.
- Normal `dotnet build` in this execution environment aborts inside the SDK's
  `Process.GetStat/GetStartTime` startup, before MSBuild. The user must produce the
  distributable net6.0 DLL using the supplied package command on their PC.
- No Unity/game process, two-peer test, native RPC timing test, or performance
  benchmark was available. Do not interpret compiler/algorithm tests as those tests.

See MULTIPLAYER-BETA.md for installation and explicit limitations, and
BETA-FEEDBACK.md for the community test/report plan. Do not ship development or
compiler-check DLLs. The packager requires an explicit `--beta` flag, a beta
version matching the protocol, and locally installed external references.
