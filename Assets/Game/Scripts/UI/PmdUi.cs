using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 UI가 함께 쓰는 부품 — 대화창, 버튼, 기술 칸 테두리, 체력바 틀.
///
/// 생김새는 <c>Assets/Game/Art/UI/</c>의 참고 그림을 따른다.
/// <list type="bullet">
/// <item><c>textbox.png</c> — 대화창. 깨끗한 픽셀 아트라 색과 두께를 그대로 옮겼다.
///   바깥에서 안으로 연청 → 밝은청(좌우 5px, 위아래 1px) → 진청, 속은 남색이다.</item>
/// <item><c>button.png</c> — 붉은 버튼. 보간된 업스케일이고 "FIGHT" 글자까지 박혀 있어
///   층 구조와 팔레트만 재서 다시 그렸다.</item>
/// <item><c>moves.png</c> — 기술 칸의 금색 베벨 테두리.</item>
/// <item><c>bars.png</c> — 체력바 틀(어두운 윤곽 + 흰 트랙)과 "HP" 꼬리표.</item>
/// </list>
///
/// 모두 9슬라이스 스프라이트다(<c>scratchpad/bake_ui.py</c>가 굽고 <c>UiSpriteSetup</c>이
/// 들여온다). 화면마다 창 크기가 다른데 테두리를 코드로 그리면 크기가 바뀔 때마다 두께와
/// 모서리를 다시 맞춰야 한다. 스프라이트 한 장이면 Unity가 알아서 늘려 준다.
///
/// 원본은 본문 폰트(24 = PMD 기본 12의 두 배)에 맞춰 <b>2배</b>로 구워 두었다. 그래서
/// 스프라이트 픽셀이 UI 단위와 1:1이고, 배율을 따로 맞출 필요가 없다.
/// </summary>
public static class PmdUi
{
    /// <summary>대화창 속 글자색. 흰색보다 살짝 눌러 남색 배경에서 눈이 편하다.</summary>
    public static readonly Color TextColor = new Color(0.97f, 0.97f, 0.94f);

    /// <summary>고른 항목의 글자. 붉은 버튼 위에서 흰색과 확실히 갈리는 연한 금색이다.</summary>
    public static readonly Color HighlightColor = new Color(1f, 0.94f, 0.63f);

    /// <summary>고를 수 없는 항목의 글자.</summary>
    public static readonly Color DisabledColor = new Color(0.62f, 0.62f, 0.6f);

    /// <summary>제목처럼 힘을 줄 때 쓰는 하늘색. 대화창 테두리와 같은 계열이다.</summary>
    public static readonly Color AccentColor = new Color(0.47f, 0.69f, 0.97f);

    /// <summary>대화창 속 남색. 테두리 스프라이트의 가운데는 비어 있고 이 색이 그 자리를 채운다.</summary>
    public static readonly Color PanelFill = new Color32(32, 72, 104, 255);

    /// <summary>대화창 테두리 두께 — 스프라이트의 9슬라이스 테두리와 같다 (좌우 14, 위아래 6).</summary>
    public static readonly Vector2 PanelInset = new Vector2(14f, 6f);

    // ---------------------------------------------------------------- 스프라이트

    private static Sprite panel, button, buttonOn, buttonOff, moveFrame, moveFrameOff, barFrame, chip;

    private static Sprite Load(ref Sprite cache, string name)
    {
        if (cache == null) cache = Resources.Load<Sprite>("UI/" + name);
        return cache;
    }

    public static Sprite PanelSprite => Load(ref panel, "PmdPanel");
    public static Sprite ButtonSprite => Load(ref button, "PmdButton");
    /// <summary>고른 칸 — 링이 금색으로 바뀐다. 붉은색만 밝히면 흘깃 봐서 구분되지 않는다.</summary>
    public static Sprite ButtonOnSprite => Load(ref buttonOn, "PmdButtonOn");
    public static Sprite ButtonOffSprite => Load(ref buttonOff, "PmdButtonOff");
    public static Sprite MoveFrameSprite => Load(ref moveFrame, "PmdMoveFrame");
    public static Sprite MoveFrameOffSprite => Load(ref moveFrameOff, "PmdMoveFrameOff");
    public static Sprite BarFrameSprite => Load(ref barFrame, "PmdBarFrame");
    /// <summary>작은 꼬리표 — 체력바의 "HP" 표와 기술 칸의 속성 표. 흰 속을 물들여 쓴다.</summary>
    public static Sprite ChipSprite => Load(ref chip, "PmdChip");

    /// <summary>9슬라이스 Image 하나. 스프라이트가 없으면 단색 사각형으로 버틴다.</summary>
    public static Image MakeSliced(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
        else
        {
            // 스프라이트를 아직 들여오지 않았어도 화면이 사라지지는 않게 한다.
            image.sprite = PrimitiveSprites.Square;
        }
        return image;
    }

    // ---------------------------------------------------------------- 대화창

    /// <summary>
    /// 대화창 한 장. 테두리와 속을 <b>따로</b> 만든다 — 테두리 스프라이트의 가운데는 비어 있고,
    /// 자식 <c>Fill</c>이 그 자리를 남색으로 채운다.
    ///
    /// 굳이 나눠 둔 이유: 속을 반투명하게 하고 싶은 창이 있다(기술 강화 팔레트는 뒤쪽 전투가
    /// 비쳐 보여야 한다). 스프라이트에 속색을 박아 두면 그럴 수가 없다.
    /// </summary>
    public static Image MakePanel(Transform parent, string name, bool opaque = true)
    {
        Image frame = MakeSliced(parent, name, PanelSprite);
        Image fill = MakeFill(frame.rectTransform);
        if (!opaque)
        {
            Color c = fill.color;
            c.a = 0.86f;
            fill.color = c;
        }
        return frame;
    }

    /// <summary>대화창 속을 채우는 남색 판. 테두리 안쪽에 1px 물려 들어가 이음매가 없다.</summary>
    public static Image MakeFill(RectTransform frame)
    {
        var go = new GameObject("Fill", typeof(RectTransform));
        // 테두리보다 먼저 그려져야 하므로 첫 자식으로 넣는다. 여러 코드가 GetChild(0)으로 찾는다.
        go.transform.SetParent(frame, false);
        go.transform.SetAsFirstSibling();
        var fill = go.AddComponent<Image>();
        fill.sprite = PrimitiveSprites.Square;
        fill.color = PanelFill;
        fill.raycastTarget = false;
        RectTransform rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(PanelInset.x - 1f, PanelInset.y - 1f);
        rt.offsetMax = new Vector2(-(PanelInset.x - 1f), -(PanelInset.y - 1f));
        return fill;
    }

    /// <summary>화면 전체를 덮는 어두운 막. 뒤의 게임 화면을 눌러 준다.</summary>
    public static Image MakeBackdrop(Transform parent, string name, float alpha = 0.82f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = PrimitiveSprites.Square;
        image.color = new Color(0.02f, 0.03f, 0.06f, alpha);
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        return image;
    }

    /// <summary>
    /// 대화창 속 한 줄. 글자색과 폰트는 여기서 통일한다.
    ///
    /// ⚠️ <b>글자에 <c>Outline</c>·<c>Shadow</c>를 더하지 말고, 글자색을 어둡게 두지 말 것.</b>
    /// PMD 비트맵 폰트(<c>Resources/Fonts/PMDFont_Atlas</c>)는 글리프마다 <b>검은 윤곽이 이미
    /// 구워져 있다</b> — 흰 속에 검은 테두리다. 어떤 바탕에도 얹을 수 있게 원작이 그렇게 만든
    /// 폰트다. 그래서
    /// <list type="bullet">
    /// <item><c>Outline</c>을 또 얹으면 그림자가 이중이 되어 획이 뭉개진다.</item>
    /// <item>어두운 색을 주면 흰 속이 검은 윤곽과 같은 밝기가 되어 글자가 덩어리로 뭉친다.
    ///   밝은 띠 위에서 특히 심하다 — 기술 칸과 체력바 꼬리표가 그래서 안 읽혔다.</item>
    /// </list>
    /// 밝은 속 + 구워진 검은 윤곽이면 바탕이 밝든 어둡든 글자 모양이 그대로 읽힌다.
    /// </summary>
    public static Text MakeText(Transform parent, string name, string body, int size,
                                TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        Text text = PixelUi.MakeText(parent, name, size, TextColor, anchor);
        text.text = body;
        return text;
    }


    // ---------------------------------------------------------------- 버튼

    /// <summary>
    /// 메뉴 한 칸 — <c>button.png</c>의 붉은 버튼이다. 창 한 장에 글자 한 줄이고,
    /// 고른 칸은 테두리 링이 금색으로 바뀌며 글자가 연한 금색이 된다.
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
            if (panel != null)
            {
                panel.sprite = !enabled ? ButtonOffSprite
                                        : (selected ? ButtonOnSprite : ButtonSprite);
                panel.color = Color.white;
            }
            if (label != null)
                label.color = !enabled ? DisabledColor
                                       : (selected ? HighlightColor : TextColor);
        }
    }

    /// <summary>
    /// 커서가 <b>새 칸에 올라선 순간</b>에만 hover 소리를 내고, 지금 칸 번호를 돌려준다.
    /// 부르는 쪽은 돌려받은 값을 다음 프레임까지 들고 있으면 된다:
    /// <code>lastHovered = PmdUi.TrackHoverSound(lastHovered, hovered);</code>
    ///
    /// 창마다 hover 판정을 따로 돌리는 구조라(이 씬에는 EventSystem이 없다) 소리를 내는
    /// 규칙도 창마다 흩어질 뻔했다. "칸이 바뀐 순간"이라는 판단만 여기 모아 둔다 —
    /// 매 프레임 부르면 커서를 올려 둔 내내 소리가 이어진다.
    ///
    /// 칸 밖으로 나가는 것(−1)은 소리를 내지 않는다. 나가는 것은 알릴 일이 아니다.
    /// </summary>
    public static int TrackHoverSound(int previous, int hovered)
    {
        if (hovered >= 0 && hovered != previous) GameAudio.PlayUiHover();
        return hovered;
    }

    /// <summary>버튼 한 칸을 만든다. 자리는 부모 rect 가운데를 기준으로 잡는다.</summary>
    public static Entry MakeEntry(Transform parent, string name, string body, int size,
                                  Vector2 anchoredPosition, Vector2 boxSize)
    {
        Entry entry = MakeButton(parent, name, body, size);
        entry.rect.anchorMin = entry.rect.anchorMax = new Vector2(0.5f, 0.5f);
        entry.rect.pivot = new Vector2(0.5f, 0.5f);
        entry.rect.sizeDelta = boxSize;
        entry.rect.anchoredPosition = anchoredPosition;
        return entry;
    }

    /// <summary>자리는 부르는 쪽이 정하는 버튼. 목록에 쌓아 놓는 카드들이 이쪽을 쓴다.</summary>
    public static Entry MakeButton(Transform parent, string name, string body, int size)
    {
        var entry = new Entry();
        entry.panel = MakeSliced(parent, name, ButtonSprite);
        entry.rect = entry.panel.rectTransform;

        entry.label = MakeText(entry.rect, name + "Label", body, size);
        Stretch(entry.label.rectTransform);
        // 글자가 링에 닿지 않게 좌우로 물러선다.
        entry.label.rectTransform.offsetMin = new Vector2(12f, 0f);
        entry.label.rectTransform.offsetMax = new Vector2(-12f, 0f);
        entry.SetSelected(false);
        return entry;
    }

    // ---------------------------------------------------------------- 꼬리표

    /// <summary>
    /// 작은 색 꼬리표 — 체력바의 "HP" 표, 기술 칸의 속성 표. 흰 속을 가진 스프라이트를
    /// 물들여 쓰므로 어두운 윤곽도 그 색의 어두운 판이 되어 저절로 어울린다.
    /// </summary>
    public static Text MakeChip(Transform parent, string name, string body, int size,
                                Color color, Color textColor)
    {
        Image box = MakeSliced(parent, name, ChipSprite);
        box.color = color;
        Text text = MakeText(box.rectTransform, name + "Label", body, size);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = textColor;
        Stretch(text.rectTransform);
        // 글자가 표 테두리에 닿지 않게 좌우로 물러선다.
        text.rectTransform.offsetMin = new Vector2(ChipPadding, 0f);
        text.rectTransform.offsetMax = new Vector2(-ChipPadding, 0f);
        CenterGlyphs(text);
        return text;
    }

    /// <summary>표·칸 안쪽 좌우 여백.</summary>
    public const float ChipPadding = 4f;

    /// <summary>
    /// 글자를 칸 가운데로 올려 앉힌다.
    ///
    /// ⚠️ uGUI는 <b>폰트가 보고하는 높이</b>로 세로 정렬을 맞추는데, PMD 비트맵 폰트는
    /// 글자칸이 12인데 ascent를 9로 보고한다 — 크기 24짜리 한 줄을 18로 보고하면서 실제로는
    /// 24를 그린다(<see cref="PixelUi.LineBoxHeight"/>에 같은 함정이 적혀 있다). 그래서
    /// 가운데 정렬을 맡기면 글자가 그 차이만큼 <b>아래로</b> 내려앉고, 글리프에 구워진 검은
    /// 윤곽까지 더해져 칸의 아래 테두리를 뚫고 나간다 — "HP"·"EXP"·"근접"이 그랬다.
    ///
    /// 보고값과 실제 칸의 차이의 절반만큼 올려 주면 눈으로 보는 가운데에 온다.
    /// 윤곽 한 픽셀만큼 더 올려 아래 테두리와 사이를 띄운다.
    /// </summary>
    public static void CenterGlyphs(Text text)
    {
        if (text == null) return;
        float reported = text.preferredHeight;
        if (reported <= 0f) return;
        float lift = Mathf.Max(0f, (text.fontSize - reported) * 0.5f) + 1f;
        RectTransform rt = text.rectTransform;
        rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y + lift);
        rt.offsetMax = new Vector2(rt.offsetMax.x, rt.offsetMax.y + lift);
    }

    // ---------------------------------------------------------------- 자리 맞추기

    /// <summary>부모를 가득 채우도록 늘린다.</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
