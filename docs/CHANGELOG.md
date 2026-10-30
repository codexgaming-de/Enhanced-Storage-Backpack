# 0.1.8-test.1 — 2026-10-30 (unpublished singleplayer test)

- Fix the drag guard treating a completed cash drag as an active drag. This could prevent storage pagination/layout until another ordinary item was dragged.
- Keep actual cash and item drags protected using the native IsCurrentlyDragging state.
- Add a targeted storage-open diagnostic for a stale cash-type flag.
- No inventory serialization or save-format changes. Native reproduction/retest is still required; see UI-CASH-DRAG-TEST.md.

# Enhanced Storage + Backpack

## 0.1.7 — Singleplayer backpack loading fix
- Fixed a reproduced backpack opening failure after loading a save with After Hours installed by restoring specialized item instances through the game's native deserializer.
- Added checks that reject empty item save data and detectable loss of specialized item data during serialization.
- Debug logging is now always enabled, including for existing configurations. The log retains up to three sessions, with an approximately 1 MiB limit per session.
- Added automatic detection by the optional Updates Checker mod (install separately).
- Removed temporary UI diagnostic retries.
- Tested by the project owner: backpack opening, saving and full restart with 36 occupied slots; all logged item IDs, quantities, qualities and packaging values matched after reload. Updates Checker recognition also confirmed.
- Existing damaged item data is not repaired. Reported storage item losses remain under investigation.
- Singleplayer / IL2CPP only. This release does not update the multiplayer beta.

## 0.1.6 — initial public release

- Nine independently configurable storage types and a backpack with 1–128 slots.
- Native storage menu with page navigation, up to 40 slots per page.
- Safe shrinking with item relocation and retention of required extra slots.
- Live MelonPreferences settings through Mod Manager & Phone App; German/English.
- Configurable backpack hotkey, B by default; police searches ignore its contents.
- Optional single diagnostic log retaining the last three logged sessions.
- Backpack persistence attached to the native Inventory subfile writer, fixing
  missing contents after returning to the main menu or restarting the game.

Manual tests confirmed by the project owner on 27 September 2026: save/load from
main menu, full restart, unsaved item moves in both directions without duplication,
save-slot isolation A → B → A, police searches, and operation on the main save.

No multiplayer support. No performance benchmarks or simulated interrupted-write
tests. The project owner confirmed the packaged DLL passed the final in-game test.
The release package also includes PERMISSIONS.md; the tested DLL is unchanged.

