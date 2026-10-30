# Employee storage slots — 0.1.9-test.1

Baseline: singleplayer 0.1.8 on fix/cash-drag-storage-ui.
Owner report: small rack with four occupied original slots is treated as full;
removing one stack allows harvesting again despite 128 configured inventory slots.
Attached logs 20261030-232551/232557 confirm ESB 0.1.8, Production Center
0.1.2-rebuild.22 and small racks resized from 4 to 128. They do not record
employee capacity decisions; the screenshot/owner report provides that evidence.

Available decompiled game reference: PlaceableStorageEntity.Start copies the
current inventory slots into separate InputSlots and OutputSlots.
ITransitEntity.GetInputCapacityForItem/InsertItemIntoInput operate on InputSlots.
ESB previously resized ItemSlots without updating these separate lists.
This explains the original-slot threshold; native confirmation of the fix is pending.

Correction: update both lists in place after grow/shrink/compaction, comparing
pointer order to avoid unnecessary writes. Reuse original slot objects and
retain locks/filters. Never clear an aliased inventory list. Request another
normal update after PlaceableStorageEntity.Start to cover initialization ordering.
No changes to station behavior, employee AI, item values or save formats.
Multiplayer is not changed by this test branch.

Validation: full-source Roslyn compiler/API check passed against available game
references with net8 reference assemblies (not a distributable net6 build).
Runtime employee behavior has not been executed here.

Test:
1. Use the same save and unchanged rack settings; confirm 0.1.9-test.1 loaded.
2. Keep the first four small-rack slots full with stacks that cannot accept
   the harvested product; leave slot 5+ empty. No need to remove a stack.
3. With ripe crop and existing employee destination, verify harvest/delivery
   into an added slot. Check quantity and quality.
4. Check a chemist delivery to an expanded rack and employee pickup from a slot
   beyond the original capacity, where the assigned route supports it.
5. Save normally, fully restart, repeat without reassigning the destination.
6. Check a safe live resize and ordinary manual storage/backpack navigation.
   Do not shrink across an active delivery for the initial test.
7. Supply Latest.log and ESB debug log. Expected diagnostic for a resized small
   rack: ESB_RACK_TRANSIT_SYNC ... inventory=128 | input=128 | output=128.

Remaining limits: no native runtime validation yet; active delivery/resizing
interleavings and other mods replacing transit lists are not established.

## Follow-up: 0.1.9-test.2 — saved extended slots

test.1 restored employee access in the user's first test, but the restart exposed a separate storage load-path defect. The uploaded SaveGame_3 contains 59 mushroom substrates in slots 11–16 of large rack ba61b2ba-b755-404a-b226-3bcb9c52b280 (Storage Unit), assigned to botanist Kevin. The save itself still contains those items. The available game reference uses ItemSet.LoadTo directly, bypassing the existing LoadFromItemSet patch; LoadTo iterates only the existing slot count.

test.2 patches the explicit list overload and expands only a supported storage's actual owned list before native restoration. It does not deserialize items itself or change the save format.

Validation performed:
- Full source compiler/API check against available IL2CPP reference assemblies passed (net8 reference compilation, not a native net6 game run).
- Read-only save fixture check: 128 saved slots, 59 substrates at slots 11–16; original 8-slot loop reaches none, expanded 128-slot loop reaches all.
- Native runtime and Harmony hook invocation still pending.

User test:
1. Close the game and build/install 0.1.9-test.2.
2. Keep the supplied ZIP unchanged. Load the original saved state represented by it; if overwritten since, restore a separate test copy from that ZIP.
3. Immediately inspect the large supply rack: slots 11–16 should contain 10/10/10/10/10/9 substrates before employee consumption. Pause employee work for exact quantity comparison.
4. Verify ESB_RACK_ITEMSET_LOAD reports serializedSlots=128 and availableSlots=128 for large racks.
5. Verify employee harvesting/delivery beyond vanilla slots still works.
6. With employee consumption paused, record quantities, save normally, fully exit, reload and compare quantities and quality.
7. Check filters and a storage with a lower configured capacity than its saved contents; occupied saved slots must not be discarded.
8. Return Latest.log and Enhanced-Storage-Backpack-Debug.log. No release promotion until native results are confirmed.

Multiplayer port is still pending.
