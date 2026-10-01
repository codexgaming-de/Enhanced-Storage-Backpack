# Beta release validation — 0.2.0-beta.2, 2026-10-01

Gameplay baseline: `fefe743c82e24f11f70f1d43adeba063a5db0acc` (tested as beta.1).
Evidence: project-owner reports and supplied diagnostic logs, 30 September–1 October.
Environment: two Steam accounts on one Nobara PC, main client as host and a
Wolf/Moonlight client; Schedule I 0.4.6f13 IL2CPP, MelonLoader 0.7.3,
SteamNetworkLib 1.6.0. This is not an independent multi-machine test.

| Check | Result |
| --- | --- |
| Host settings handshake and native reply roundtrip | Confirmed by both logs |
| Client backpack deposit, withdrawal, internal moves | User passed |
| Split/count check: 10 total, 6 hotbar / 4 backpack | User passed every step |
| Host save, reload and rejoin | 6/4 distribution restored |
| Exit without saving | Saved distribution restored; no duplicates reported |
| Rejoin while host remains in game | Current distribution retained |
| Live backpack 80 → 100 → 80 with item in slot 100 | User passed; item retained and accessible |
| Medium rack 50 slots / 5 rows → 16 / 2 | User passed; both peers consistent |
| Host/client personal backpacks and reload | Contents remained separate |
| Concurrent access to same storage/stack | Live updates and total quantity preserved |
| Host save during repeated backpack transfers | User passed including reload |
| Client transfer timing | 153–221 ms total; final response processing 1–2 ms |
| Exact beta.2 ZIP/DLL | Build and smoke test on owner's PC still required |

No runtime transfer behavior was changed for beta.2. Confirmation remains automatic;
client prediction was explicitly deferred until community feedback. Connection traces
are opt-in at 15-second intervals; receive traces are capped and transfer timing is
opt-in. All use the existing single debug log retaining three logged sessions.

Remaining unverified: separate-PC/WAN latency and packet loss, 3–4 players,
forced crashes during saves, cash-specific native transfers, broad mod combinations,
and general FPS/memory benchmarking. A legacy empty-inventory diagnostic can still
emit ESB_MP_host-offer-rejected during join; it is not the Steam handshake result.
Do not treat that message alone as proof of handshake failure.

Beta release gate: build the net6.0 package, verify its checksum, install the DLL
extracted from that exact ZIP on both peers, check join/transfer/save/reload, then
upload as an optional experimental file. Keep stable 0.1.6 available.

---

## Historical validation records (superseded where noted above)

# Beta validation — 0.2.0-beta.1, 2026-09-29

The historical 0.1.6 report below applies to the stable singleplayer release only.
It does not validate the new multiplayer runtime.

| Check | Result |
| --- | --- |
| Full source reference compile | Zero errors; 14 CS1701 .NET 6/8 reference warnings |
| Multiplayer protocol | 862 assertions passed |
| Host settings handshake | 23 assertions passed |
| Host backpack core | 3,069 assertions passed, including repeated invariant checks |
| Remote backpack preservation | 17 assertions passed |
| Native-independent transfer rules | 26 scenarios passed |
| Native channel probe protocol | 16 assertions passed |
| Snapshot chunk/integrity protocol | 47 assertions passed |
| Remote session journal | 50 assertions passed; isolated filesystem exercised |
| New command/receipt/cash rules | 51 assertions passed |
| Beta packager | Six tests passed with simulated build output |
| Normal SDK/net6.0 build here | Blocked by SDK Process.GetStat/GetStartTime startup exception |
| Real host/client gameplay | Not performed; community beta validation pending |
| Native UI, RPC detours, timing, cash replication | Compiled, not executed in-game here |
| Performance benchmark / process-crash recovery | Not performed |

The reference-check DLL is not packaged or distributed. The release script builds
net6.0 on the user's PC and records hashes. Packaging tests validate allowlisting,
manifest and failure paths; their simulated DLL is not a usable mod binary.
Follow-up: direct MSBuild.dll invocation also fails during process inspection.
Reset now preserves nested cancellation state; existing foreign storage slots are
not rejected solely for exceeding ESB’s capacity limit.
Stable main is kept separate. No claim of bug-free multiplayer or atomic world saves.

---

# Validation — 0.1.6

Updated 27 September 2026. Runtime code baseline:
773316a0c30a31739e97aeb304d94df19e666983.

Status: normal gameplay acceptance tests passed; release package and exact packaged DLL validated.
No runtime code changed during this documentation and packaging preparation.

| Criterion | Evidence | Status |
| --- | --- | --- |
| Nine storage types; separate slots/rows, resizing, item access and visuals | User's incremental in-game tests | Passed |
| Live settings and DE/EN switching | User tests with Manager 2.2.4 | Passed |
| Backpack hotkey, slots, navigation, layout | User tests | Passed |
| Save → main menu → load | User and 0.1.6 log: payload present, occupied=2 | Passed |
| Save → complete restart → load | User and new 0.1.6 session log: occupied=2 | Passed |
| Unsaved moves both directions; no duplication | User confirmation 13:03 Berlin | Passed |
| Save A → B → A; main save | User confirmation 13:18 Berlin | Passed |
| Police search ignores backpack | User confirmation | Passed |
| Single log retains three logged sessions | Automated filesystem tests and prior user tests | Passed |
| Size/page rules | 32,779 automated checks | Passed |
| Backpack JSON codec | 272 automated checks for 0.1.6 | Passed |
| Compiler check | Direct Roslyn compile; 12 known CS1701 reference warnings | Passed with environment limitation |
| Regular net6.0 DLL running in game | User builds and tests 0.1.6 | User confirmed |
| Exact packaged DLL installation and checksum | Uploaded ZIP checksum, manifest and CRC verified; user confirmed packaged DLL test | Passed |
| Interrupted writes / internal runtime shutdown | No fault injection in game | Not run |
| FPS / memory measurement | No benchmark | Not run |
| Multiplayer | Outside agreed scope | Not applicable |

Local validation uses .NET 8 reference assemblies with supplied IL2CPP assemblies;
its compiler-check DLL must never be distributed. The normal MSBuild path was
blocked here by a Process.GetStat/GetStartTime environment error. Use the regular
net6.0 Release build on the user's PC for distribution.

Older save claims were superseded by the 0.1.4/0.1.5 failure. The 0.1.6 log and
subsequent explicit tests establish the ordinary save/load result above.
See BUG-BACKPACK-PERSISTENCE.md for the investigation history.

Automated tests are reproducible through the three test projects listed in README.
They do not execute native game methods. A passing codec test alone does not prove
native save integration, fault recovery, or atomicity of the entire game save.

Release gates: build package using scripts/package-release.py, inspect its contents,
test its extracted DLL, confirm current screenshots and upload permissions, then
publish. Keep the untested fault/performance cases disclosed; do not claim universal
failure recovery, bug-free operation or measured FPS improvements.

## Packaging preparation checks

Script syntax and CLI checked. Isolated tests with a simulated build verified:
failed build creates no ZIP; only allowlisted files enter the ZIP even when other
DLLs/logs exist in the build output; an existing package is refused and preserved.
These are packaging tests, not a successful game-DLL build. A fresh regular build
attempt in this environment still exits 134 in System.Diagnostics.Process.GetStat
before compilation. The actual release ZIP must therefore be built on the user's PC.

## Final package

The project owner confirmed final packaged-DLL testing and availability of screenshots.
For publication, PERMISSIONS.md and the updated changelog were added to the reviewed
package and its manifest/checksum regenerated. The DLL bytes remain unchanged:
`428cf929ea8bfb86b975aa204361c65248b5fa8ba97e6a1136c226eb8a0489b2`.
No game code was changed or rebuilt for this documentation-only repack.
