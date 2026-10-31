# 0.1.9 — 2026-10-31 (singleplayer)

- Fixed a specific cause of items disappearing after loading a save: stored items beyond the original storage capacity could be skipped. Additional slots now exist before saved items and slot filters are restored.
- Fixed employees treating expanded storage as full at its original capacity. Employee input/output lists now follow the actual storage slots.
- Includes the storage pagination/layout fix after cash transfers from 0.1.8.
- Owner-confirmed tests passed for item restoration, saving/full restart and employee use on 0.1.9-test.2. Version 0.1.9 changes release metadata only.
- Items still present in saved data can load again. Items already absent from saved data are not recreated; not every possible cause of item loss is claimed fixed.
- Singleplayer only. Multiplayer is a separate beta download.


## Historical development notes

# 0.1.9-test.1 — 2026-10-31 (singleplayer test)

## 0.1.9-test.2 — Extended storage load-path test

- Expand supported storage inventories before the native ItemSet.LoadTo(List<ItemSlot>) restoration path used by placeable storage loaders.
- Preserve saved items and slot filters beyond vanilla capacity; defer any configured shrinking until restoration finishes.
- Keep the employee input/output synchronization from test.1.
- Add ESB_RACK_ITEMSET_LOAD diagnostics. Only the actual inventory list of supported placeable storage is eligible; station and employee inventories are excluded.
- Validation: full source API/compiler check passed against available IL2CPP references. Supplied save contains 59 mushroom substrates in slots 11–16 of a 128-slot large rack; an 8-slot restore loop cannot reach them. Native IL2CPP restoration is pending user verification. No release or multiplayer validation claimed.


- Synchronize supported storage employee input/output slot lists with the actual inventory after resizing or compaction.
- Request synchronization after native placeable-storage initialization; preserve native list identity and existing slot objects, filters and locks.
- Add ESB_RACK_TRANSIT_SYNC diagnostics. Native employee harvesting/delivery validation pending.
- Includes the 0.1.8 cash-drag UI fix; no save format changes.

# 0.1.8 — 2026-10-30

- Fix oversized storage grids and missing page navigation after moving cash, including employee locker payments.
- Correct the active-drag check so a stale cash-type flag no longer blocks storage/backpack UI or pending settings changes.
- Keep real cash and item drags protected; no save-format changes.
- Owner-confirmed reproduction test passed on 0.1.8-test.1: immediate rack layout after cash transfers, all pages and backpack opening. Version 0.1.8 changes release metadata only; exact packaged DLL smoke test remains pending.

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

