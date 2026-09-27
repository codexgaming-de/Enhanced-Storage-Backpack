using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Items;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedStorageBackpack;

internal sealed class BackpackMenu
{
    private readonly Settings settings;
    private readonly RackMenu capacity;
    private StorageMenu? menu;
    private BackpackOwner? owner;
    private readonly PagedMenuChrome chrome = new();
    private GridLayoutGroup.Constraint oldConstraint;
    private int oldCount, page;

    public bool IsOpen { get; private set; }
    public BackpackMenu(Settings settings) { this.settings = settings; capacity = new RackMenu(settings); }

    public void Open(BackpackOwner inventory)
    {
        menu = StorageMenu.Instance;
        owner = inventory;
        page = 0;
        oldConstraint = menu.SlotGridLayout.constraint;
        oldCount = menu.SlotGridLayout.constraintCount;
        capacity.Prepare(owner.ItemSlots.Count);
        IsOpen = true;
        try
        {
            menu.Open(owner.Cast<IItemSlotOwner>(), settings.Text("Rucksack", "Backpack"), "",
                (Il2CppSystem.Action)Closed);
            Bind();
        }
        catch { Closed(); throw; }
    }
    public void Close() { if (IsOpen && menu != null) menu.Close(); Closed(); }
    public void Closed()
    {
        if (!IsOpen) return;
        IsOpen = false;
        chrome.Restore();
        if (menu != null)
        {
            menu.SlotGridLayout.constraint = oldConstraint;
            menu.SlotGridLayout.constraintCount = oldCount;
        }
        owner = null;
    }
    public void ClearBindings()
    {
        if (menu == null || !IsOpen) return;
        foreach (var ui in menu.SlotsUIs) ui.ClearSlot();
    }
    public void Bind()
    {
        if (!IsOpen || owner == null || menu == null || RackStorage.Dragging) return;
        int pages = Math.Max(1, (owner.ItemSlots.Count + 39) / 40);
        page = Math.Clamp(page, 0, pages - 1);
        int start = page * 40, count = Math.Min(40, owner.ItemSlots.Count - start);
        capacity.Prepare(count);
        var visible = new Il2CppSystem.Collections.Generic.List<ItemSlot>();
        for (int i = 0; i < menu.SlotsUIs.Length; i++)
        {
            var ui = menu.SlotsUIs[i];
            ui.ClearSlot();
            ui.gameObject.SetActive(i < count);
            if (i < count) { ui.AssignSlot(owner.ItemSlots[start + i]); visible.Add(owner.ItemSlots[start + i]); }
        }
        menu.TitleLabel.text = settings.Text("Rucksack", "Backpack");
        menu.SubtitleLabel.text = settings.Text($"{owner.ItemSlots.Count} Plätze", $"{owner.ItemSlots.Count} slots");
        menu.SlotGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        menu.SlotGridLayout.constraintCount = Math.Min(8, Math.Max(1, count));
        ItemUIManager.Instance.EnableQuickMove(visible);
        LayoutRebuilder.MarkLayoutForRebuild(menu.SlotContainer);
        chrome.Show(menu, page, pages, ChangePage);
    }
    public void LayoutTick() => chrome.Tick();
    private void ChangePage(int direction)
    {
        if (RackStorage.Dragging || !IsOpen) return;
        page += direction;
        Bind();
    }
    public void Dispose()
    {
        Close();
        chrome.Dispose();
    }
}
