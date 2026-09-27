# Validation — 0.1.6

Updated 27 September 2026. Runtime code baseline:
773316a0c30a31739e97aeb304d94df19e666983.

Status: normal gameplay acceptance tests passed; release package validation pending.
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
| Exact packaged DLL installation and checksum | Package still to build on user PC | Not run |
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
