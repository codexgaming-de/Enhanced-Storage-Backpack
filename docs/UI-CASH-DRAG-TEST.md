# Singleplayer storage UI after cash drag — 0.1.8-test.1

Baseline: fix/after-hours-item-loading-test, 0.1.7.
Test branch: fix/cash-drag-storage-ui. No multiplayer port or release upload yet.

## Evidence and hypothesis

Owner screenshots show 128 slots overflowing horizontally, untranslated rack title
and no pagination; moving an ordinary item restores the 40-slot page and navigation.
Debug log Enhanced-Storage-Backpack-Debug(20261030-121229).log:
- 13:09:53 Kevin's Locker; 13:09:56 Large Storage Rack; layout only at 13:10:02.
- 13:11:16 Kevin's Locker; 13:11:21 Large Storage Rack; layout only at 13:11:44.

RackMenu.Bind exits while RackStorage.Dragging is true. The old guard used
IsCurrentlyDragging OR IsDraggingCash. In the available decompiled game reference,
StartDrag assigns draggedSlot and the cash-type flag; EndCashDrag clears draggedSlot
without clearing that flag. Cash quick-moves likewise clear the slot. Starting a
non-cash drag resets the type flag. This explains the reported recovery pattern.

The incorrect guard is established by source inspection. Its role in this exact
IL2CPP run remains to be confirmed in-game: the supplied log does not record drag flags.
Alternative timing/other-mod layout interference has not been ruled out by a native run.

## Change

Use IsCurrentlyDragging (actual dragged slot) for both item and cash drags.
Do not mutate native flags, slot ownership, items or saved data.
Log ESB_STORAGE_IDLE_CASH_FLAG at supported storage opening only when the cash-type
flag is set with no actual drag, to confirm the mechanism in the affected environment.

## Validation

Both baseline and changed full source compile with the local Roslyn compiler and
available IL2CPP/MelonLoader references, using net8 reference assemblies. This is
an API/compiler check, not a distributable net6 build or Unity runtime test.
No native gameplay was executed here. Build net6 on the owner's game-equipped PC.
No synthetic test is presented as proof of native drag lifecycle behavior.

## Manual regression test

1. Confirm 0.1.8-test.1 in Latest.log. Keep the existing slot/row settings.
2. Open Kevin's Locker, drag cash into it, release, then close.
3. Immediately open a 128-slot Large Storage Rack without moving an ordinary item.
   Expect 40 visible slots, localized title, page 1/4 and navigation immediately.
4. Visit all pages (last page has 8 slots); close and repeat three times.
5. Repeat with cash quick-move and a cancelled cash drag. Also test a normal item drag.
6. Open B after the cash action; verify backpack layout and normal transfer behavior.
7. While genuinely dragging cash or an item, pagination must remain blocked.
8. Check quantities before/after. A normal save/reload can be tested after the UI checks;
   no save is required merely to reproduce this display bug.
9. Provide Latest.log plus the ESB debug log. If the stale-cash diagnostic appears
   followed immediately by ESB_RACK_MENU, the intended path was exercised.

Status: targeted test candidate; native confirmation pending.
