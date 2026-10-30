# 0.2.0-beta.5 — 2026-10-30 (community-test beta)

- Correct the drag-state check used by storage and backpack UI after cash transfers. A completed cash drag could leave pagination/layout blocked until another ordinary item was moved.
- Keep actual cash and item drags protected; retain host authority and synchronization guards.
- Add a targeted diagnostic when a stale cash-type flag is ignored on storage opening.
- The equivalent singleplayer fix passed the owner's reproduction test. This multiplayer update still requires community host/client testing.
- Host and every client must use beta.5 with SteamNetworkLib 1.6.0 IL2CPP.
- No save-format changes or recovery of previously lost items.

# 0.2.0-beta.4 — release candidate (unpublished)

- Use native item deserialization for local backpacks and remote player restoration, preserving specialized item types.
- Reject empty item JSON and detectable serialized subtype loss before local saves or remote transfer snapshots.
- Keep bounded diagnostics permanently enabled, including with old DebugLogging settings.
- Add NexusModID 2734 metadata for optional Updates Checker recognition.
- Owner-confirmed host + one client tests: backpack opening/transfers, item quantities and quality, rejoin, world save/full restart, and a shared Large Storage Rack with 128 slots.
- Test environment: Schedule I 0.4.7f12, MelonLoader 0.7.3, SteamNetworkLib 1.6.0 IL2CPP; two accounts on one Nobara PC with Wolf/Moonlight.
- All peers require beta.4. The final packaged DLL still needs a smoke test before upload.
- Already damaged item data is not repaired. These tests do not establish that every previously reported storage-loss scenario or mod conflict is resolved.

# 0.2.0-beta.3 — 2026-10-12

- Fix the ambiguous Harmony target for PlayerManager.TryGetPlayerData on Schedule I 0.4.7.
- Adapt remote player restoration and inventory baseline reads to FullPlayerData.
- Accept SteamNetworkLib.dll and SteamNetworkLib-IL2Cpp.dll during building and packaging.
- Require identical beta.3 builds on host and clients through the existing version handshake.
- Stable singleplayer 0.1.6 is unchanged.

Initialization confirmed on 0.4.7f12 with the hotfix code before the version-only rebuild.
Multiplayer transfers, rejoining and save/load after this hotfix await community testing.
The project owner chose community validation after confirming successful initialization.

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
