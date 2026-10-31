# 0.1.9 — 2026-10-31 (singleplayer)

- Fixed a specific cause of items disappearing after loading a save: stored items beyond the original storage capacity could be skipped. Additional slots now exist before saved items and slot filters are restored.
- Fixed employees treating expanded storage as full at its original capacity. Employee input/output lists now follow the actual storage slots.
- Includes the storage pagination/layout fix after cash transfers from 0.1.8.
- Owner-confirmed tests passed for item restoration, saving/full restart and employee use on 0.1.9-test.2. Version 0.1.9 changes release metadata only.
- Items still present in saved data can load again. Items already absent from saved data are not recreated; not every possible cause of item loss is claimed fixed.
- Singleplayer only. Multiplayer is a separate beta download.


Evidence: owner reported all requested tests passed on 2026-10-31.
Enhanced-Storage-Backpack-Debug(20261030-235732).log shows two 0.1.9-test.2
sessions with ESB_RACK_ITEMSET_LOAD: serializedSlots=128, before=8,
availableSlots=128 and regular world saves. Latest(20261030-235734).log
contains no errors/exceptions; known Il2CppInterop signature warning remains.
User confirmed items, quantities and employee behavior after reload.
No claim that every employee/storage/mod combination has been tested.

0.1.9 promotion changes version and release documentation only. Full-source
reference compiler/API check passed; native net6 package build and exact ZIP DLL
smoke test remain on the user's installation.

## Historical validation

# Validation — 0.1.7 preparation

Tested gameplay baseline: 1abcfb5afdf3dac169790263629a11a400c49b83.
User logs dated 2026-10-23 13:40–13:43 show marker native-deserializer-release-prep.1,
36 occupied slots saved and reloaded, identical logged ID/type/quantity/quality/packaging,
and successful opening after restart. No ESB errors. Other Interop and translation
errors remain in Latest.log; the whole mod environment is not error-free.
Updates Checker recognition confirmed separately by the user.

0.1.7 preparation changes version strings and documentation only.
Reference compilation passes; actual net6.0 packaging and a smoke test of the
DLL extracted from that archive remain pending on the user's game installation.
Storage item loss reports and already damaged saves are not resolved by this release.
No multiplayer validation or beta port is included.

## Historical validation (0.1.6)

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
