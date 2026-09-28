# Multiplayer development — 0.2.0-dev.1

28 September 2026. Baseline: 3ecac4bd16a633a9f261dbe2ba89ee9d4a4b6a9e.
Branch: feature/0.2.0-multiplayer-beta. Stable 0.1.6 remains on main.

## Current milestone: transport preparation, not a playable beta

Implemented:
- Versioned, bounded host configuration envelope with a fresh session token.
- Nine storage slot/row pairs and backpack capacity from the host.
- Metadata attached at native Player.ReceivePlayerData entry and generated
  Target/Observers writer boundaries; repeat attachment is idempotent.
- Metadata removed and validated at RpcLogic___ReceivePlayerData before vanilla
  inventory loading. Neither vanilla fields nor the existing backpack key is removed.
- Own debug log observes player start, inventory reads/writes and save request
  boundaries. At most one line per stage/role/peer per load session; at most 64
  peer labels. No names, platform IDs, contents or save paths in these new markers.
- Client interaction remains disabled. Offers do not grant inventory access.
- Stable save payload version remains 1. No additional inventory file or autosave.

This snapshot does not synchronize live settings, transfer items, authenticate a
completed bidirectional handshake, persist remote backpacks, or protect simultaneous
storage mutations. It must NOT be uploaded as the promised playable multiplayer beta.
There is no claim that matching protocol metadata establishes trust or ownership.

## Confirmed architecture / evidence

This is an external net6.0 MelonLoader IL2CPP mod, not a Unity Editor project.
There are no Assets/Packages/ProjectSettings or connected Editor/Play Mode.
Unity Editor version and render pipeline are not established by this repository.
Available game references expose FishNet runtime and native storage RPC interfaces.

Inspected mod files: Mod.cs, Backpack.cs, BackpackOwner.cs, BackpackSave.cs,
RackStorage.cs, Settings.cs, EnhancedStorageBackpack.csproj.
Inspected supplied native wrappers (not redistributed): Player, PlayerManager,
PlayerData, StorageEntity, ISaveable, InstanceFinder.

Confirmed interfaces:
- Player inventory is represented by _inventory; PlayerCode identifies players.
- RequestSavePlayer / generated server logic and ReturnSaveRequest exist.
- ReceivePlayerData accepts PlayerData plus inventory JSON and other player data.
- Storage has slot-indexed item/quantity/filter/lock RPCs and CurrentPlayerAccessor.
- Stable persistence hooks GetInventoryString, ISaveable.WriteSubfile and LoadInventory.
- Stable rack shrinking swaps ItemSlot references locally. This is not a network
  transaction and cannot be independently repeated on clients.

Unknown: actual native call ordering, bypassed detours, remote save timing, joining
and disconnect timing, and atomicity relative to vanilla inventory transfers.
The supplied interop assemblies expose signatures, not native implementation bodies.
The 0.1.6 save bug demonstrated why hook installation alone is insufficient proof.

## Next implementation gates

1. Compile on Nobara and confirm native patch installation plus solo regression.
2. Establish a bounded bidirectional handshake bound to the real connection owner;
   reject version mismatch and stale session traffic before granting access.
3. Host-controlled storage capacities with pre-snapshot allocation on join, stable
   slot indices, synchronized resize/compaction, and access/drag coordination.
4. Per-player backpack transfers coupled to the matching player inventory snapshot.
   No independently persisted backpack state. Validate server authority and identity.
5. Save barrier/reconnect handling: never write mismatched hotbar/backpack revisions;
   do not silently report a completed save when a required peer snapshot is missing.
6. Solo protocol/state tests, normal native regression, then separate community beta.

Both host and clients will need the same eventual beta. Stable DLL and beta DLL
must never be installed together. The host will determine shared capacity; language
and hotkey remain local preferences. Rollback: return to 0.1.6 and the pre-test save.

## Local test for this development snapshot

Use a separate test save or a backup. Close the game before replacing the DLL.
Build (do not use the stable packaging script for this prerelease):

```bash
cd /home/codex/Enhanced-Storage-Backpack
git fetch origin
git switch --track origin/feature/0.2.0-multiplayer-beta
dotnet build -c Release -p:GameDirectory="/home/codex/Schreibtisch/Schedulue 1 Plugins/"
```

Replace only EnhancedStorageBackpack.dll in Mods with bin/Release/net6.0 output.
Enable our debug logging before loading the test save.
1. Startup should report 0.2.0-dev.1 and no ESB_INITIALIZATION error.
2. Open backpack and a storage; transfer items and test pagination.
3. Save, return to main menu, reload; verify both hotbar and backpack quantities.
4. Exit fully and reload. Check unsaved transfer rollback in both directions.
5. Send UserData/Enhanced-Storage-Backpack-Debug.log and the build output.

Look for ESB_MP_PATH lines (player-start, inventory-subfile-write and save paths).
A host-offer event may not occur in a solo session; its absence is not proof of
failure. Real transport and remote-player behavior need two actual peers later.

## Validation in development environment

- Direct Roslyn compile with supplied game references: passed; known net8/net6
  reference warnings remain. This compiler-check DLL is not distributable.
- Pure protocol suite: 862 assertions passed, including 1..128 capacities,
  duplicate attachment, malformed/mismatched messages, preserving hotbar and
  backpack JSON, and vanilla saves without metadata.
- Existing backpack codec suite: 272 assertions passed.
- No native gameplay, real two-peer traffic or disconnect test executed here.
- These tests exercise pure rules, not simulated Unity/FishNet peers.

Run locally: dotnet run --project tests/MultiplayerProtocolTests.csproj
