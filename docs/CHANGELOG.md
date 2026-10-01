# 0.2.0-beta.2 — 2026-10-01

- Fix host/client handshake by reading the active Steam lobby service ID.
- Receive backpack replies and inventory receipts at the native RPC readers.
- Preserve existing client slots/UI bindings after transfers; update changed slots only.
- Keep automatic host confirmation; no client-side prediction or approval dialog.
- Owner-confirmed two-client tests: item counts, save/load, unsaved rollback,
  rejoin, personal backpack isolation, live backpack/storage resizing,
  concurrent storage access, and saving during transfers.
- Keep opt-in connection/transfer diagnostics for community feedback.
- Stable singleplayer 0.1.6 remains a separate download.

Tested gameplay baseline: `fefe743c82e24f11f70f1d43adeba063a5db0acc`.
Beta.2 changes release metadata/documentation/packaging only. The exact packaged
DLL still needs a smoke test before upload. Cash-specific and adverse-network
scenarios are not covered by these user confirmations.

# 0.2.0-beta.1 — 2026-09-28 (experimental multiplayer)

- Connect client backpack UI to authenticated host move requests and acknowledgements.
- Prepare native move/split/merge/swap and cash transfers before changing host inventory.
- Add ordered hotbar receipts, duplicate-response handling and abort recovery.
- Retain current remote backpack/inventory state in host session memory for rejoin.
- Save coupled remote state only during regular world saves; gate saves on synchronization.
- Synchronize all nine storage layouts, items, locks and filters from the host.
- Freeze client interaction briefly during storage reconfiguration and saves.
- Keep language, hotkey and logging local; preserve backpack pagination after moves.
- Add explicit beta packaging, dependency checks and community feedback documentation.
- Stable singleplayer 0.1.6 remains available separately.

Status: reference compiler and automated algorithm tests passed. Native multiplayer
and the distributable net6.0 package are not validated in this environment.

# Enhanced Storage + Backpack

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
