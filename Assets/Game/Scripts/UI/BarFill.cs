using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력·경험치 바 한 줄. <c>Assets/Game/Art/UI/bars.png</c>의 생김새를 그대로 옮긴 부품이다.
///
/// 세 겹이다 — 어두운 윤곽과 흰 트랙을 가진 <b>틀</b>(9슬라이스), 그 안을 왼쪽부터 채우는
/// <b>몸통</b>, 그리고 몸통 위쪽에 얹혀 같은 만큼만 차는 <b>짙은 줄</b>. 짙은 줄이 없으면
/// 바가 종이처럼 납작해 보인다 — 원작이 굳이 두 톤으로 그린 이유다.
///
/// 색은 부르는 쪽이 정한다. 체력은 초록, 경험치는 푸른색이며, 짙은 줄은 그 색을 어둡게
/// 한 값이라 색을 하나만 넘겨도 두 톤이 맞아떨어진다.
/// </summary>
public class BarFill : MonoBehaviour
{
    /// <summary>틀 스프라이트의 9슬라이스 두께. 채움은 이만큼 물러나 앉는다.</summary>
    private const float FrameInset = 2f;

    /// <summary>짙은 줄이 차지하는 세로 비율. bars.png는 채움 세 줄 중 위 한 줄이 짙다.</summary>
    private const float ShadeFraction = 0.34f;

    /// <summary>몸통을 어둡게 해 짙은 줄을 만드는 배수.</summary>
    private const float ShadeMultiplier = 0.62f;

    // ---------------------------------------------------------------- 좌하단 두 바의 공통 배치
    //
    // 체력바와 경험치바가 나란히 놓이므로 꼬리표 칸 폭을 함께 써야 두 바의 왼쪽 끝이 맞는다.
    // "HP"와 "EXP"는 글자 수가 달라서, 칸을 글자에 맞추면 바가 어긋난다.

    /// <summary>
    /// 꼬리표 칸의 폭. 가장 긴 글자("EXP", 세 글자)가 <see cref="ChipFontSize"/>로 들어가는 값이다.
    /// "HP"는 두 글자라 좌우가 조금 남지만, 칸 폭을 글자마다 달리하면 두 바의 왼쪽 끝이 어긋난다.
    /// </summary>
    public const float ChipWidth = 72f;

    /// <summary>꼬리표와 바 사이 간격.</summary>
    public const float ChipGap = 4f;

    /// <summary>바가 시작하는 x. 꼬리표 칸을 비켜 앉는다.</summary>
    public const float BarOffsetX = ChipWidth + ChipGap;

    /// <summary>꼬리표 글자 크기. PMD 폰트라 12의 배수여야 한다.</summary>
    public const int ChipFontSize = 24;

    public RectTransform Root { get; private set; }

    private Image body;
    private Image shade;

    /// <summary>바 하나를 만든다. 자리와 크기는 <see cref="Root"/>에 대고 부르는 쪽이 정한다.</summary>
    public static BarFill Create(Transform parent, string name, Color color)
    {
        Image frame = PmdUi.MakeSliced(parent, name, PmdUi.BarFrameSprite);
        var bar = frame.gameObject.AddComponent<BarFill>();
        bar.Root = frame.rectTransform;

        bar.body = MakeSlice(bar.Root, "Body", 0f, 1f);
        bar.shade = MakeSlice(bar.Root, "Shade", 1f - ShadeFraction, 1f);

        bar.SetColor(color);
        bar.SetRatio(1f);
        return bar;
    }

    /// <summary>
    /// 왼쪽부터 차는 조각 하나. 세로는 <paramref name="bottom"/>~<paramref name="top"/> 비율만 차지한다.
    ///
    /// 크기를 직접 줄이지 않고 <see cref="Image.Type.Filled"/>를 쓰는 이유: 크기를 줄이면
    /// 자식 앵커가 함께 움직여 왼쪽 끝이 흔들린다. 채움 비율은 그림만 잘라 낸다.
    /// </summary>
    private static Image MakeSlice(RectTransform parent, string name, float bottom, float top)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = PrimitiveSprites.Square;
        image.raycastTarget = false;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = new Vector2(0f, bottom);
        rt.anchorMax = new Vector2(1f, top);
        // 위아래로는 비율 앵커를 쓰므로 테두리만큼만 물러난다. 짙은 줄은 위쪽이 테두리에 닿는다.
        rt.offsetMin = new Vector2(FrameInset, bottom <= 0f ? FrameInset : 0f);
        rt.offsetMax = new Vector2(-FrameInset, -FrameInset);
        return image;
    }

    /// <summary>
    /// 바 왼쪽에 붙는 꼬리표("HP", "EXP"). 글자가 칸을 채우도록 크게 넣는다 —
    /// 작게 넣으면 색 조각만 보이고 무슨 바인지 읽히지 않는다.
    /// </summary>
    public static Text MakeChip(Transform parent, string name, string label,
                                Color box, Color ink, float height)
    {
        Text text = PmdUi.MakeChip(parent, name, label, ChipFontSize, box, ink);
        RectTransform rt = (RectTransform)text.transform.parent;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(ChipWidth, height);
        rt.anchoredPosition = Vector2.zero;
        return text;
    }

    public void SetColor(Color color)
    {
        if (body != null) body.color = color;
        if (shade != null)
            shade.color = new Color(color.r * ShadeMultiplier, color.g * ShadeMultiplier,
                                    color.b * ShadeMultiplier, color.a);
    }

    /// <summary>남은 비율(0~1)을 반영한다.</summary>
    public void SetRatio(float ratio)
    {
        float clamped = Mathf.Clamp01(ratio);
        if (body != null) body.fillAmount = clamped;
        if (shade != null) shade.fillAmount = clamped;
    }
}
