# Multiplayer development — 0.2.0-dev.2

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

1. Verify SteamNetworkLib initialization and existing solo behavior on Nobara.
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
