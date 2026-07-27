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
        // preferredHeight는 위 폭이 정해진 뒤에야 올바른 줄 수를 반영한다.
        float height = text.preferredHeight;
        rt.sizeDelta = new Vector2(-padding * 2f, height);
        return top - height;
    }
}
