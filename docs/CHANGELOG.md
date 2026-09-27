# Enhanced Storage + Backpack

## 0.1.6 — initial public release candidate

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
tests. Packaging and final testing of the exact distributed DLL remain release gates.
