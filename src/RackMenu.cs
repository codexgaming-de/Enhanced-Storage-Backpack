using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Items;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedStorageBackpack;

internal sealed class RackMenu
{
    private readonly Settings settings;
    private StorageMenu? menu;
    private GridLayoutGroup.Constraint constraint;
    private int constraintCount;
    private bool customized;
    public RackMenu(Settings settings) => this.settings = settings;

    public bool IsShowing(StorageEntity entity) => StorageMenu.InstanceExists && StorageMenu.Instance.IsOpen &&
        StorageMenu.Instance.OpenedStorageEntity != null && StorageMenu.Instance.OpenedStorageEntity.Pointer == entity.Pointer;

    public void Prepare(int count)
    {
        var current = StorageMenu.Instance;
        if (menu == null || menu.Pointer != current.Pointer)
        {
            menu = current;
            constraint = menu.SlotGridLayout.constraint;
            constraintCount = menu.SlotGridLayout.constraintCount;
            customized = false;
        }
        if (current.SlotsUIs.Length >= count) return;
        var expanded = new Il2CppReferenceArray<ItemSlotUI>(count);
        for (int i = 0; i < current.SlotsUIs.Length; i++) expanded[i] = current.SlotsUIs[i];
        for (int i = current.SlotsUIs.Length; i < count; i++)
        {
            var slot = UnityEngine.Object.Instantiate(ItemUIManager.Instance.ItemSlotUIPrefab, current.SlotContainer);
            slot.ClearSlot();
            slot.gameObject.SetActive(false);
            expanded[i] = slot;
        }
        current.SlotsUIs = expanded;
    }

    public void ClearBindings()
    {
        if (menu == null) return;
        foreach (var slot in menu.SlotsUIs) slot.ClearSlot();
    }

    public void Bind(StorageEntity entity)
    {
        if (!IsShowing(entity) || SmallRackStorage.Dragging) return;
        Prepare(entity.ItemSlots.Count);
        var current = menu!;
        for (int i = 0; i < current.SlotsUIs.Length; i++)
        {
            var ui = current.SlotsUIs[i];
            ui.ClearSlot();
            ui.gameObject.SetActive(i < entity.ItemSlots.Count);
            if (i < entity.ItemSlots.Count) ui.AssignSlot(entity.ItemSlots[i]);
        }
        if (string.Equals(entity.StorageEntityName, "Small Storage Rack", StringComparison.OrdinalIgnoreCase))
            current.TitleLabel.text = settings.Text("Kleines Lagerregal", "Small Storage Rack");
        var grid = current.SlotGridLayout;
        int count = entity.ItemSlots.Count;
        int rows = Math.Clamp(entity.DisplayRowCount, 1, Math.Max(1, count));
        int columns = (count + rows - 1) / rows;
        // No global canvas update or immediate recursive layout rebuild.
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = rows;
        // Native Open initializes cell dimensions. Do not replace them with the
        // pre-open grid size, which may be zero while the menu is still inactive.
        settings.Trace($"ESB_RACK_LAYOUT | cell={grid.cellSize.x}x{grid.cellSize.y} | container={current.SlotContainer.rect.width}x{current.SlotContainer.rect.height}");
        LayoutRebuilder.MarkLayoutForRebuild(current.SlotContainer);
        customized = true;
        if (ItemUIManager.InstanceExists) ItemUIManager.Instance.EnableQuickMove(entity.ItemSlots);
        settings.Trace($"ESB_RACK_MENU | slots={count} | rows={rows} | columns={columns}");
    }

    public void Restore()
    {
        if (menu == null || !customized) return;
        menu.SlotGridLayout.constraint = constraint;
        menu.SlotGridLayout.constraintCount = constraintCount;
        customized = false;
    }
}
