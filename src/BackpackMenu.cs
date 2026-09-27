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
    private GameObject? navigation;
    private Button? previous, next;
    private TextMeshProUGUI? pageLabel;
    private GridLayoutGroup.Constraint oldConstraint;
    private int oldCount, page;
    private bool positionPending;
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
        if (navigation != null) navigation.SetActive(false);
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
        EnsureNavigation();
        navigation!.SetActive(pages > 1);
        pageLabel!.text = $"{page + 1} / {pages}";
        previous!.interactable = page > 0;
        next!.interactable = page + 1 < pages;
        positionPending = true;
    }
    // Run once on the next update, after Unity's normal layout pass.
    public void LayoutTick()
    {
        if (!positionPending || !IsOpen || navigation == null || menu == null) return;
        positionPending = false;
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue;
        foreach (var ui in menu.SlotsUIs)
        {
            if (!ui.gameObject.activeSelf) continue;
            var rect = ui.GetComponent<RectTransform>();
            var p = rect.localPosition;
            left = Math.Min(left, p.x + rect.rect.xMin);
            right = Math.Max(right, p.x + rect.rect.xMax);
            bottom = Math.Min(bottom, p.y + rect.rect.yMin);
        }
        if (left != float.MaxValue)
            navigation.transform.localPosition = new Vector3((left + right) / 2, bottom - 12, 0);
    }
    private void ChangePage(int direction)
    {
        if (RackStorage.Dragging || !IsOpen) return;
        page += direction;
        Bind();
    }
    private static GameObject UiObject(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(new[] { Il2CppType.Of<RectTransform>() }));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }
    private TextMeshProUGUI Label(GameObject parent, string text)
    {
        var go = UiObject("Label", parent.transform, Vector2.zero, parent.GetComponent<RectTransform>().sizeDelta);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = menu!.TitleLabel.font;
        label.fontSize = 22;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }
    private Button MakeButton(string name, string text, float x, Action click)
    {
        var go = UiObject(name, navigation!.transform, new Vector2(x, 0), new Vector2(48, 32));
        var image = go.AddComponent<Image>(); image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        var button = go.AddComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)click);
        Label(go, text);
        return button;
    }
    private void EnsureNavigation()
    {
        if (navigation != null && navigation.transform.parent == menu!.SlotContainer) return;
        if (navigation != null) UnityEngine.Object.Destroy(navigation);
        navigation = UiObject("ESB_BackpackPages", menu!.SlotContainer, new Vector2(0, -12), new Vector2(200, 32));
        navigation.AddComponent<LayoutElement>().ignoreLayout = true;
        previous = MakeButton("Previous", "<", -76, () => ChangePage(-1));
        next = MakeButton("Next", ">", 76, () => ChangePage(1));
        var labelObject = UiObject("Page", navigation.transform, Vector2.zero, new Vector2(90, 32));
        pageLabel = Label(labelObject, "");
    }
    public void Dispose()
    {
        Close();
        if (navigation != null) UnityEngine.Object.Destroy(navigation);
        navigation = null;
    }
}
