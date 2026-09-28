# Multiplayer development — 0.2.0-dev.3

Branch: feature/0.2.0-multiplayer-beta. Stable 0.1.6 remains on main.

## Implemented in dev.2

- SteamNetworkLib 1.6.0 IL2CPP as an external dependency. Not bundled or modified.
- Dedicated reliable P2P channel 21837 and namespaced message key.
- No mod-created lobby: membership and host identity are obtained from the existing
  game lobby and Steam's lobby owner, additionally checked against FishNet server role.
- Bounded hello / offer / acknowledgement / ready handshake, matching build,
  client nonce, host session and increasing configuration revision.
- Sender identity comes from the actual transport callback, never a JSON sender field.
- Only the host supplies nine storage slot/row pairs and backpack capacity.
- Session-only client settings; local MelonPreferences are not overwritten.
- Host changes are picked up within the two-second control interval and sent to peers.
  Local host resizing retains its existing update timing. No per-frame config sends.
- Own log: ESB_STEAM_READY, ESB_HOST_SETTINGS, ESB_HANDSHAKE_CLIENT, failures.
- Native inventory transport prototype removed from outgoing RPCs. The receiving
  stripper remains for compatibility with dev.1. No new save file or autosave.

**Client inventory access remains blocked.** This is not yet the public playable beta.
Slot allocation, synchronized compaction, item transfer authority and remote backpack
persistence are not complete. A completed settings handshake alone does not unlock them.
Language, keybind and diagnostics remain local; capacity settings are host-controlled.

## Dependency setup

Author: Bars Studio / ifBars. Nexus: https://www.nexusmods.com/schedule1/mods/1396
Release: https://github.com/ifBars/SteamNetworkLib/releases/tag/v1.6.0
Documentation: https://ifbars.github.io/SteamNetworkLib/docs/getting-started.html

Install the IL2CPP package from Nexus into the game's UserLibs directory.
The required reference/runtime path is `UserLibs/SteamNetworkLib.dll`.
If using the GitHub DLL named `SteamNetworkLib-IL2Cpp.dll`, rename that downloaded
file to `SteamNetworkLib.dll` in UserLibs. Do not install two copies. Do not use Mono.
The library stays separately installed and is excluded from our release ZIP.
Current developer documentation specifies runtime-matching build references;
the Nexus description's Mono-reference instruction disagrees with that documentation.
We compiled against the released v1.6.0 IL2CPP binary, not an invented API or stub.
The upstream repository contains the MIT licence; Nexus's default permissions differ.
No upstream source or binary is redistributed by this branch.

## Confirmed local baseline — dev.1

The owner tested and supplied Enhanced-Storage-Backpack-Debug(20260928-111123).log.
28 September 2026, 13:05 and 13:10 Berlin sessions initialized dev.1 successfully.
At 13:08:39 the backpack snapshot had 80 slots / 2 occupied slots.
Main-menu reload at 13:09:15 and full-restart reload at 13:10:35 restored 80 / 2.
No errors in that supplied log. Player start and actual Inventory writer hooks ran.
No remote transport, remote save or exact item quantities were established by that log.

## Architecture and unknowns

This is an external net6.0 MelonLoader IL2CPP mod, not a Unity Editor project.
No connected Editor/Play Mode; Unity version/render pipeline not established here.
Game networking uses FishNet. Inspected native wrappers: Player, PlayerManager,
PlayerData, StorageEntity, ISaveable, InstanceFinder and Lobby.

Player has RequestSavePlayer, ReceivePlayerData, GetInventoryString, LoadInventory
and WriteData. Storage exposes slot-indexed item/quantity/filter/lock RPCs and
CurrentPlayerAccessor. Stable rack shrinking swaps local ItemSlot references;
this cannot be independently repeated on clients. Native implementations/call ordering
are not visible in the supplied interop wrappers and require real runtime evidence.

## Next gates

1. DONE locally: dev.2 SteamNetworkLib initialization and solo save/reload verified below.
2. Host-controlled storage allocation on clients BEFORE native item RPCs, stable
   indices and synchronized compaction; coordinate active access and dragging.
3. Per-player backpack transactions coupled to the same player inventory snapshot.
4. Save barrier and reconnect handling without mismatched hotbar/backpack revisions.
5. Solo rule tests and regression, then separately labelled community multiplayer beta.

Both host and clients require the same eventual beta and dependency. Never install
stable and development mod DLLs together. Use a backup or separate test save.
Rollback: restore the stable DLL and pre-test save. Do not distribute compiler-check DLLs.

## Local test for dev.2

After installing SteamNetworkLib, close the game:

```bash
cd /home/codex/Enhanced-Storage-Backpack
git switch feature/0.2.0-multiplayer-beta
git pull --ff-only origin feature/0.2.0-multiplayer-beta
dotnet build -c Release -p:GameDirectory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
```

Replace Mods/EnhancedStorageBackpack.dll with the resulting net6.0 DLL.
Keep our debug logging enabled before startup.
- Confirm ESB_READY 0.2.0-dev.2 and ESB_STEAM_READY, no initialization errors.
- Load test save, change host rack slots/rows and backpack capacity; check normal UI.
- Save, reload from main menu, then fully restart and reload.
- Send own debug log and build output. No second peer is needed for this local test.
- Without a client, ESB_HANDSHAKE_CLIENT is not expected.

## Validation

Direct Roslyn compile against supplied references and released SteamNetworkLib:
passed with 12 known net8/net6 CS1701 warnings and no new compiler warnings/errors.
862 pure protocol assertions and 23 host-handshake assertions passed.
Assertions cover wrong sender, cross-player tokens, stale revisions, out-of-order
confirmation, duplicate messages, altered same-revision settings, version mismatch,
rejoin/host-change rejection, payload bounds and preserving inventory JSON.
No native Steam traffic, two-peer gameplay, disconnect or remote persistence test
has been run here. Handshake tests do not simulate Unity or Steam peers.

`dotnet run --project tests/HostSettingsTests.csproj`
`dotnet run --project tests/MultiplayerProtocolTests.csproj`

## Host transaction core (after dev.2 local test)

`HostBackpackState` is a host-thread-only, pure C# foundation, **not yet wired to
Steam messages, native ItemSlot changes, UI or disk writes**. The transaction core remains disconnected from native item mutations;
client access stays blocked. Do not describe this as implemented remote persistence.

- Per-player inventory/backpack pair; caller must supply authenticated transport identity.
- Host-issued session and reconnect lease, monotonic sequence and revision checks.
- Requests identify slots only; they cannot supply fabricated item contents.
- Whole-stack move to an empty slot; exact JSON retained, duplicate command idempotent.
- Host compaction protects occupied slots when requested capacity is insufficient.
- Save barrier captures both containers at one revision and freezes mutations until
  the exact save ticket completes. No automatic or independent backpack file writes.
- Reconnect retains live host state; a new loaded game uses a fresh state/session.
- Defensive snapshots; bounded player/slot counts. No polling or networking in this core.

Integration still MUST provide native lock/filter checks, stack splitting/merging,
quantity validation, authorization of all other inventory mutations (use, pickup,
trade and storage), authenticated peer/player mapping, and a confirmed native save
completion/failure path. The core is not safe to expose to clients until those adapters
exist. Storage transfers are not implemented by this core. A save ticket alone does
not make the game's filesystem writes atomic. Tests simulate state reload from the
captured pair; they do not test native game persistence or a connection drop.

`dotnet run --project tests/HostBackpackTests.csproj`
3028 assertions passed, including 1000 transfers with duplicate packets and item
conservation, identity rejection, stale state, safe shrinking, save freeze, reconnect,
and discarding unsaved moves on simulated reload.

The supplied 20260928-124026 log confirms dev.2 at 14:36/14:39 Berlin, library
1.6.0.0 initialized, small closet 10/1 and medium closet 20/2 applied immediately.
Save 14:38:07, main-menu reload 14:38:35 and restart reload 14:39:35 show 80 backpack
slots / 2 occupied. No errors recorded. No remote settings handshake is present.
The stale 0.1.6 settings-snapshot log label now uses MultiplayerProtocol.Build.

Next concrete integration gate: establish how remote Player inventory mutations and
RequestSavePlayer/PlayerManager.SavePlayer are ordered on the native host. The supplied
interop assemblies expose declarations and native invocations, not those method bodies.
Do not remove the client gate based on the pure-core tests alone.

## dev.3 — native remote save preservation

The owner supplied the Mono reference assembly from Steam's alternate branch:
SHA256 50ad999a4ddaf35940fe6e2bc126317cbf05de7c59d55d51ad5b1f849c33d7ac.
It is used only for inspection, never compiled into or distributed with this mod.
The runtime and build references remain IL2CPP. Corresponding Mono and IL2CPP method
signatures were checked; matching every native runtime behavior is not established.

Findings from method bodies:
- Player.GetInventoryString serializes the host's nine _inventory slots via ItemSet.
- Player.LoadInventory only applies on the owner; it treats cash separately and
  handles eight hotbar indices. It is NOT a host setter for remote inventories.
- SetInventoryItem's server reader checks connection ownership; its logic replaces
  one slot, with no ESB revision or coupled-backpack validation.
- PlayerManager.WriteData queues player saves and adds new player records before
  the queued writes complete.
- RequestSavePlayer server logic invokes PlayerManager.SavePlayer, which can write
  a player independently of a full world save. This must not become an ESB autosave.
- PlayerManager.TryGetPlayerData reads saved inventory from disk. Its true return
  means player data exists; it does NOT guarantee Inventory was read successfully.

Implemented preservation only:
- Before native PlayerManager save methods modify loaded-player records, load each
  remote backpack baseline through the game's host-side save lookup.
- Both GetInventoryString and the actual Inventory WriteSubfile boundary retain that
  baseline inside CURRENT native inventory JSON. No separate files or writes added.
- Never replace current hotbar with the old save's hotbar. Missing existing inventory,
  unsupported/corrupt backpack data, conflicts and uncached identities fail the save.
- New and vanilla players retain normal inventory JSON without an ESB extension.
- Cache cleared at StartGame; bounded to 64 identities; never populated from network
  payloads. Existing client gameplay gate remains intact.

17 pure preservation assertions pass; direct compile against the supplied IL2CPP
references and SteamNetworkLib passes with 13 CS1701 reference-version warnings.
This is not the live remote transaction/save-barrier integration: no client backpack
moves are enabled or persisted. HostBackpackState is still a separate tested core.
Native multiplayer preservation needs community runtime testing once the complete
beta is available. No additional solo test is requested just for this boundary.

Next: a native inventory adapter must serialize all ESB transfers with vanilla
SetInventoryItem updates, enforce item locks/filters and stack rules, and acknowledge
both ends before saving. Merely sending periodic backpack snapshots on another
transport would not establish ordering against the game's hotbar updates.

## Transfer adapter work after dev.3 (step 1 still in progress)

Inspected the Mono ItemUIManager and HotbarSlot implementations. SlotClicked performs
quick-move, while EndDrag performs an ordinary drop. Starting a drag only changes
presentation; it does not remove the source item. Cash uses separate balance logic.

Added BackpackTransferRules and NativeBackpackTransfer:
- Host-provided slot rules support full/partial moves, bounded merges and full swaps.
- Native adapter checks hard/player filters, source removal and target addition locks,
  and both reverse directions for swaps. Rechecks contents, locks, filters and capacity
  before mutation. Linked sibling slots and cash slots/items are rejected for now.
- Copies native item subclasses before mutation; internal slot setters avoid recursive
  owner RPCs. On a setter failure both original slots are attempted for restoration;
  caller must quarantine interaction on failure, especially failed rollback.
- Tests cover 26 transfer-rule scenarios, including single-item transfers, full targets,
  swap filters/locks, invalid quantities and overflow. Native Unity setters, copy
  metadata fidelity and rollback callbacks cannot be executed in the plain-C# tests.
- IL2CPP reference compile succeeds; 13 CS1701 reference-version warnings remain.

**Not active in gameplay yet.** These classes are not wired to the UI or P2P callbacks.
Do not call NativeBackpackTransfer directly from a message without resolving and
validating the sender's owned slots, session/revision, inventory lease and save barrier.
Do not describe these rules as completed network synchronization. Build stays dev.3.

Before enabling step 1, finish reliable request/acknowledgement and native RPC ordering:
Steam P2P messages and vanilla FishNet SetInventoryItem use independent transports;
a Steam confirmation alone does not establish that earlier vanilla slot updates have
arrived. Any ordering fence must be validated on the native reliable channel. Cash
balance movement, quick-move batching, timeout recovery and client UI bindings remain.
No new solo test or DLL replacement is needed for these currently unconnected classes.
