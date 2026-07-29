using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 픽셀 UI 공통 규칙. PMD 비트맵 폰트(`Resources/Fonts/PMDFont`)를 코드에서 만드는 UI에도 쓰기 위한 접근자와,
/// 글자가 뭉개지지 않게 하는 크기 규칙을 한곳에 모아 둔다.
///
/// 폰트가 비트맵이라 확대 배율이 정수가 아니면 획 굵기가 들쭉날쭉해진다. 그래서
/// 폰트 크기는 반드시 <see cref="BaseFontSize"/>(12)의 배수여야 하고, 캔버스 배율도 정수여야 한다.
/// </summary>
public static class PixelUi
{
    /// <summary>PMDFont의 기준 크기. 이 값의 배수로만 폰트 크기를 정해야 픽셀이 깨지지 않는다.</summary>
    public const int BaseFontSize = 12;

    /// <summary>캔버스 배율 1단계에 해당하는 화면 높이. 1080 미만에서는 항상 1배.</summary>
    public const int ReferenceHeight = 1080;

    private static Font font;
    private static Material worldMaterial;

    /// <summary>UI Text용 PMD 비트맵 폰트. 없으면 내장 폰트로 대체한다.</summary>
    public static Font Font
    {
        get
        {
            if (font == null)
            {
                font = Resources.Load<Font>("Fonts/PMDFont");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return font;
        }
    }

    /// <summary>월드 공간 TextMesh용 머티리얼. UI/Default는 캔버스 밖에서 쓰기 부적절하다.</summary>
    public static Material WorldFontMaterial
    {
        get
        {
            if (worldMaterial == null) worldMaterial = Resources.Load<Material>("Fonts/PMDFont_World");
            return worldMaterial;
        }
    }

    /// <summary>현재 화면에서 쓸 정수 캔버스 배율.</summary>
    public static int PixelScale => Mathf.Max(1, Screen.height / ReferenceHeight);

    /// <summary>가장 가까운 <see cref="BaseFontSize"/> 배수로 맞춘다 (최소 1배).</summary>
    public static int SnapFontSize(int size) =>
        Mathf.Max(BaseFontSize, Mathf.RoundToInt(size / (float)BaseFontSize) * BaseFontSize);

    /// <summary>
    /// PMD 폰트를 쓰는 Text를 만든다. 크기는 항상 12의 배수로 맞춰진다.
    /// 가로는 부모 폭에 맞춰 늘어나고 넘치면 줄바꿈한다.
    /// </summary>
    public static Text MakeText(Transform parent, string name, int size, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Font;
        text.fontSize = SnapFontSize(size);
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>
    /// 테두리가 있는 어두운 패널을 만든다. 반환값은 바깥 테두리의 RectTransform이고,
    /// 안쪽 채움은 자식으로 붙어 있으므로 내용은 반환값 아래에 그대로 넣으면 된다.
    /// </summary>
    public static RectTransform MakePanel(Transform parent, string name, int borderWidth = 3)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image border = go.AddComponent<Image>();
        border.color = new Color(0.78f, 0.72f, 0.5f, 1f);
        border.raycastTarget = false;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(go.transform, false);
        Image fill = fillGo.AddComponent<Image>();
        fill.color = new Color(0.05f, 0.06f, 0.1f, 0.96f);
        fill.raycastTarget = false;
        RectTransform fillRt = fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(borderWidth, borderWidth);
        fillRt.offsetMax = new Vector2(-borderWidth, -borderWidth);

        return border.rectTransform;
    }

    /// <summary>부모 폭에 좌우 여백을 두고 붙는, 위에서 아래로 쌓는 배치를 설정한다.</summary>
    public static float StackFromTop(Text text, float top, float padding)
    {
        RectTransform rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-padding * 2f, 0f);
        rt.anchoredPosition = new Vector2(0f, top);
        // 줄 수는 위 폭이 정해진 뒤에야 올바르게 나온다.
        float height = LineBoxHeight(text);
        rt.sizeDelta = new Vector2(-padding * 2f, height);
        return top - height;
    }

    /// <summary>
    /// 글자가 실제로 차지하는 세로 폭. <see cref="Text.preferredHeight"/>를 쓰면 안 된다.
    ///
    /// preferredHeight는 줄당 폰트의 ascent만 센다. PMD 폰트는 ascent가 9, 글자칸이 12라
    /// 크기 48짜리 한 줄을 36으로 보고하는데 실제로는 48을 그린다. 그 값으로 쌓으면
    /// 줄마다 25%씩 모자라서 아래 요소와 글자가 겹친다.
    /// 그래서 줄 수만 생성기에서 얻고 높이는 폰트의 줄 간격으로 직접 계산한다.
    /// </summary>
    public static float LineBoxHeight(Text text) =>
        text == null ? 0f : LineBoxHeight(text, text.rectTransform.rect.width);

    /// <summary>
    /// 줄바꿈 폭을 직접 주는 판. 아직 배치되지 않은 Text는 <c>rect.width</c>가 0이라
    /// 한 글자에 한 줄씩 세어 버린다. 칸 크기를 글자에 맞춰 <b>정하는</b> 쪽에서는
    /// 폭을 먼저 알고 있으므로 그 값을 넘긴다.
    /// </summary>
    public static float LineBoxHeight(Text text, float width)
    {
        if (text == null || string.IsNullOrEmpty(text.text)) return 0f;

        Font f = text.font;
        // 동적 폰트는 요청한 크기 그대로 렌더링되므로 preferredHeight가 맞다.
        if (f == null || f.dynamic || f.fontSize <= 0) return text.preferredHeight;

        TextGenerator gen = text.cachedTextGeneratorForLayout;
        gen.Populate(text.text, text.GetGenerationSettings(new Vector2(width, 0f)));

        float scale = text.fontSize / (float)f.fontSize;
        return Mathf.Max(1, gen.lineCount) * f.lineHeight * scale;
    }
}
