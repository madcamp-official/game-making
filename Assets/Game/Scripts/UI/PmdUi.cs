using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 전체 화면(타이틀·캐릭터 선택·안내·결과)이 함께 쓰는 창 부품.
///
/// 생김새는 <c>Assets/Game/Art/UI/dialogue.png</c>의 파란 대화창 — PMD 하늘의 탐험대 —
/// 을 그대로 따른다. 픽셀을 읽어 뽑은 테두리는 바깥에서 안으로 연청 → 밝은청 → 청색 밴드
/// → 남색이고 속은 거의 검다. 그 결을 9슬라이스 스프라이트 한 장(<c>Resources/UI/PmdPanel</c>)
/// 으로 굳혀 두었으므로, 창을 어떤 크기로 늘려도 테두리 두께가 변하지 않는다.
///
/// 9슬라이스로 만든 이유: 화면마다 창 크기가 다른데 테두리를 코드로 그리면 크기가 바뀔 때마다
/// 두께와 모서리를 다시 맞춰야 한다. 스프라이트 한 장이면 Unity가 알아서 늘려 준다.
/// </summary>
public static class PmdUi
{
    /// <summary>대화창 속 글자색. 흰색보다 살짝 눌러 남색 배경에서 눈이 편하다.</summary>
    public static readonly Color TextColor = new Color(0.97f, 0.97f, 0.94f);

    /// <summary>고른 항목을 가리키는 노란색. 원작의 커서 색이다.</summary>
    public static readonly Color HighlightColor = new Color(1f, 0.86f, 0.28f);

    /// <summary>고를 수 없는 항목. 글자만 눌러 두고 창은 그대로 둔다.</summary>
    public static readonly Color DisabledColor = new Color(0.55f, 0.58f, 0.62f);

    /// <summary>제목처럼 힘을 줄 때 쓰는 하늘색. 테두리와 같은 계열이다.</summary>
    public static readonly Color AccentColor = new Color(0.47f, 0.69f, 0.97f);

    private const int SliceBorder = 6;

    private static Sprite panelSprite;

    /// <summary>
    /// 창 스프라이트. <c>Resources</c>에 두는 이유: 화면을 코드로 짜므로 씬에 물려 둘 자리가
    /// 없고, 그렇다고 화면마다 인스펙터 참조를 만들면 화면을 하나 추가할 때마다 씬을 건드려야 한다.
    /// </summary>
    public static Sprite PanelSprite
    {
        get
        {
            if (panelSprite == null) panelSprite = Resources.Load<Sprite>("UI/PmdPanel");
            return panelSprite;
        }
    }

    /// <summary>화면을 가득 채우는 그릇. 그 아래에 창을 놓는다.</summary>
    public static RectTransform MakeFullScreen(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        Stretch(rt);
        return rt;
    }

    /// <summary>대화창 한 장. <paramref name="opaque"/>가 거짓이면 속을 살짝 비쳐 보이게 한다.</summary>
    public static Image MakePanel(Transform parent, string name, bool opaque = true)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = PanelSprite;
        image.type = Image.Type.Sliced;
        // 스프라이트 원본이 16px인데 9슬라이스 테두리가 6px이라, 픽셀 그대로 그리면
        // 화면이 커질수록 테두리가 실처럼 얇아진다. 화면 배율만큼 곱해 두께를 지킨다.
        image.pixelsPerUnitMultiplier = 1f / Mathf.Max(1, PixelUi.PixelScale);
        if (!opaque) image.color = new Color(1f, 1f, 1f, 0.93f);
        return image;
    }

    /// <summary>화면 전체를 덮는 어두운 막. 뒤의 게임 화면을 눌러 준다.</summary>
    public static Image MakeBackdrop(Transform parent, string name, float alpha = 0.82f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.02f, 0.03f, 0.06f, alpha);
        Stretch(image.rectTransform);
        return image;
    }

    /// <summary>대화창 속 한 줄. 글자색과 폰트는 여기서 통일한다.</summary>
    public static Text MakeText(Transform parent, string name, string body, int size,
                                TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        Text text = PixelUi.MakeText(parent, name, size, TextColor, anchor);
        text.text = body;
        return text;
    }

    /// <summary>
    /// 메뉴 한 칸. 창 한 장에 글자 한 줄이고, 고른 항목만 테두리를 밝히고 글자를 노랗게 한다.
    ///
    /// uGUI 버튼을 쓰지 않는 이유: 이 씬에는 <c>EventSystem</c>이 없다(좌클릭이 공격이라
    /// 넣으면 게임 입력과 겹친다 — <c>RelicTooltip</c>과 같은 사정이다). 마우스 위치를
    /// 사각형과 직접 견줘 판정한다.
    /// </summary>
    public class Entry
    {
        public Image panel;
        public Text label;
        public RectTransform rect;
        public bool enabled = true;

        /// <summary>마우스가 이 칸 위에 있는가. 화면 좌표를 그대로 받는다.</summary>
        public bool Contains(Vector2 screenPoint) =>
            RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);

        /// <summary>고른 상태를 겉모습에 반영한다.</summary>
        public void SetSelected(bool selected)
        {
            if (!enabled)
            {
                panel.color = new Color(0.62f, 0.62f, 0.66f, 0.85f);
                label.color = DisabledColor;
                return;
            }
            panel.color = selected ? new Color(1f, 1f, 1f) : new Color(0.78f, 0.82f, 0.9f);
            label.color = selected ? HighlightColor : TextColor;
        }
    }

    /// <summary>메뉴 한 칸을 만든다. 자리는 부모 rect 안의 위쪽부터 쌓는 쪽이 다루기 쉽다.</summary>
    public static Entry MakeEntry(Transform parent, string name, string body, int size,
                                  Vector2 anchoredPosition, Vector2 size2)
    {
        var entry = new Entry();
        entry.panel = MakePanel(parent, name);
        entry.rect = entry.panel.rectTransform;
        entry.rect.anchorMin = entry.rect.anchorMax = new Vector2(0.5f, 0.5f);
        entry.rect.pivot = new Vector2(0.5f, 0.5f);
        entry.rect.sizeDelta = size2;
        entry.rect.anchoredPosition = anchoredPosition;

        entry.label = MakeText(entry.rect, name + "Label", body, size);
        Stretch(entry.label.rectTransform);
        entry.SetSelected(false);
        return entry;
    }

    /// <summary>부모를 가득 채우도록 늘린다.</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>화면을 코드로 짜는 화면들이 공유하는 캔버스. 항상 게임 화면 위에 덮는다.</summary>
    public static Canvas MakeCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        go.AddComponent<CanvasScaler>();
        return canvas;
    }
}
