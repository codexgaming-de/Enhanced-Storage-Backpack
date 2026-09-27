using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedStorageBackpack;

// Shared layout for backpack and paged racks. All native positions are restored on close.
internal sealed class PagedMenuChrome
{
    private StorageMenu? menu;
    private GameObject? navigation;
    private Button? previous, next;
    private TextMeshProUGUI? pageLabel;
    private Action<int>? changePage;
    private readonly List<(RectTransform Rect, Vector3 Position, Vector3 Scale)> originals = new();
    private bool pending, active;
    private int requestedFrame;

    public void Show(StorageMenu current, int page, int pages, Action<int> change)
    {
        if (!active || menu == null || menu.Pointer != current.Pointer)
        {
            Restore();
            menu = current;
            foreach (var rect in new[] { current.TitleLabel.rectTransform, current.SubtitleLabel.rectTransform,
                current.SlotContainer, current.CloseButtonContainer })
                originals.Add((rect, rect.localPosition, rect.localScale));
            active = true;
        }
        changePage = change;
        EnsureNavigation();
        navigation!.SetActive(pages > 1);
        pageLabel!.text = $"{page + 1} / {pages}";
        previous!.interactable = page > 0;
        next!.interactable = page + 1 < pages;
        pending = true;
        requestedFrame = Time.frameCount;
    }

    private static Bounds BoundsOf(RectTransform canvas, RectTransform rect)
    {
        // CalculateRelativeRectTransformBounds is stripped in the game's IL2CPP build.
        // Compute the same local-space bounds from rect geometry and native transforms.
        Bounds result = default;
        bool initialized = false;
        foreach (var child in rect.GetComponentsInChildren<RectTransform>(false))
        {
            var r = child.rect;
            for (int corner = 0; corner < 4; corner++)
            {
                var local = new Vector3((corner & 1) == 0 ? r.xMin : r.xMax,
                    (corner & 2) == 0 ? r.yMin : r.yMax, 0);
                var point = canvas.InverseTransformPoint(child.TransformPoint(local));
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }

    private static void MoveBoundsTop(RectTransform canvas, RectTransform rect, Bounds bounds, float x, float y)
    {
        var delta = canvas.TransformVector(new Vector3(x - bounds.center.x, y - bounds.max.y, 0));
        rect.position += delta;
    }

    public void Tick()
    {
        // Let Unity calculate its grid once; do not force a recursive canvas rebuild.
        if (!active || !pending || menu == null || Time.frameCount == requestedFrame) return;
        pending = false;
        var canvas = menu.Canvas.GetComponent<RectTransform>();
        var title = menu.TitleLabel.rectTransform;
        var subtitle = menu.SubtitleLabel.rectTransform;
        var slots = menu.SlotContainer;
        var close = menu.CloseButtonContainer;
        var titleBounds = BoundsOf(canvas, title);
        var subtitleBounds = BoundsOf(canvas, subtitle);
        // SlotContainer itself can have size zero. Measure actual active slot rectangles.
        Bounds slotBounds = default;
        bool found = false;
        foreach (var ui in menu.SlotsUIs)
        {
            if (!ui.gameObject.activeSelf) continue;
            var bounds = BoundsOf(canvas, ui.GetComponent<RectTransform>());
            if (!found) { slotBounds = bounds; found = true; }
            else slotBounds.Encapsulate(bounds);
        }
        if (!found) return;
        var closeBounds = BoundsOf(canvas, close);
        float navHeight = navigation != null && navigation.activeSelf
            ? BoundsOf(canvas, navigation.GetComponent<RectTransform>()).size.y + 12 : 0;
        float height = titleBounds.size.y + 8 + subtitleBounds.size.y + 18 + slotBounds.size.y +
            14 + navHeight + 12 + closeBounds.size.y;
        float x = canvas.rect.center.x;
        float top = Math.Min(canvas.rect.yMax - 60, canvas.rect.center.y + height / 2 + 25);
        MoveBoundsTop(canvas, title, titleBounds, x, top);
        top -= titleBounds.size.y + 8;
        MoveBoundsTop(canvas, subtitle, subtitleBounds, x, top);
        top -= subtitleBounds.size.y + 18;
        MoveBoundsTop(canvas, slots, slotBounds, x, top);
        top -= slotBounds.size.y + 14;
        if (navigation != null && navigation.activeSelf)
        {
            var rect = navigation.GetComponent<RectTransform>();
            MoveBoundsTop(canvas, rect, BoundsOf(canvas, rect), x, top);
            top -= navHeight;
        }
        top -= 12;
        // Re-measure because parents may have moved with other elements above.
        MoveBoundsTop(canvas, close, BoundsOf(canvas, close), x, top);
    }

    public void Restore()
    {
        if (navigation != null) navigation.SetActive(false);
        foreach (var entry in originals)
            if (entry.Rect != null) { entry.Rect.localPosition = entry.Position; entry.Rect.localScale = entry.Scale; }
        originals.Clear();
        active = pending = false;
        changePage = null;
    }
    public void Dispose()
    {
        Restore();
        if (navigation != null) UnityEngine.Object.Destroy(navigation);
        navigation = null;
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
        go.GetComponent<RectTransform>().anchorMin = go.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
        var image = go.AddComponent<Image>(); image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        var button = go.AddComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)click);
        Label(go, text);
        return button;
    }
    private void EnsureNavigation()
    {
        if (navigation != null && navigation.transform.parent == menu!.Container) return;
        if (navigation != null) UnityEngine.Object.Destroy(navigation);
        navigation = UiObject("ESB_StoragePages", menu!.Container, new Vector2(0, -12), new Vector2(200, 32));
        navigation.AddComponent<LayoutElement>().ignoreLayout = true;
        previous = MakeButton("Previous", "<", -76, () => changePage?.Invoke(-1));
        next = MakeButton("Next", ">", 76, () => changePage?.Invoke(1));
        var labelObject = UiObject("Page", navigation.transform, Vector2.zero, new Vector2(90, 32));
        labelObject.GetComponent<RectTransform>().anchorMin = labelObject.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
        pageLabel = Label(labelObject, "");
    }
}
