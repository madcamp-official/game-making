using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력바 바로 아래에 붙는 얇은 경험치 바. 전투방 하나를 치울 때마다 절반씩 찬다.
///
/// 레벨 숫자는 일부러 띄우지 않는다. 여기서 알아야 하는 건 "다음 기술 강화까지 얼마 남았나"뿐이고,
/// 숫자를 같이 띄우면 체력 숫자와 섞여 읽는 데 방해가 된다.
/// </summary>
public class ExpBar : MonoBehaviour
{
    private const float MarginX = 30f;
    /// <summary>둘 중 아래 칸. 높이와 여백은 <see cref="BarFill"/>이 한 곳에서 정한다.</summary>
    private const float MarginY = BarFill.BottomMargin;
    private const float BarWidth = 300f;
    private const float BarHeight = BarFill.BarHeight;

    /// <summary>
    /// 경험치는 푸른색이다 — <c>bars.png</c>의 "Exp. Point bar colors"에서 그대로 잰 값.
    /// 체력의 초록과 색으로 갈려서, 두 바가 나란히 있어도 어느 쪽인지 바로 읽힌다.
    /// </summary>
    private static readonly Color FillColor = new Color32(73, 146, 251, 255);

    /// <summary>
    /// "EXP" 꼬리표. 체력의 호박색 표와 같은 형식이고 색만 바의 푸른색을 따른다.
    /// 바보다 한 단계 짙게 둔다 — 흰 글자가 묻히지 않을 만큼이어야 한다.
    /// </summary>
    private static readonly Color ChipColor = new Color32(44, 104, 208, 255);
    private static readonly Color ChipInk = new Color32(240, 248, 255, 255);

    private PlayerLevel level;
    private BarFill bar;

    public static ExpBar Create(Transform canvasRoot)
    {
        var go = new GameObject("ExpBar", typeof(RectTransform));
        RectTransform root = (RectTransform)go.transform;
        root.SetParent(canvasRoot, false);
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(MarginX, MarginY);
        root.sizeDelta = new Vector2(BarFill.BarOffsetX + BarWidth, BarHeight);

        ExpBar bar = go.AddComponent<ExpBar>();

        BarFill.MakeChip(root, "Chip", "EXP", ChipColor, ChipInk, BarHeight);

        bar.bar = BarFill.Create(root, "Bar", FillColor);
        RectTransform barRt = bar.bar.Root;
        barRt.anchorMin = barRt.anchorMax = new Vector2(0f, 0.5f);
        barRt.pivot = new Vector2(0f, 0.5f);
        barRt.sizeDelta = new Vector2(BarWidth, BarHeight);
        barRt.anchoredPosition = new Vector2(BarFill.BarOffsetX, 0f);

        return bar;
    }

    private void Start() => Bind();

    private void Update()
    {
        // 플레이어가 아직 없거나 재시작으로 교체되면 다시 잡는다.
        if (level == null) Bind();
    }

    private void Bind()
    {
        PlayerLevel found = PlayerLevel.Instance;
        if (found == null || found == level) return;

        if (level != null) level.OnProgressChanged -= Refresh;
        level = found;
        level.OnProgressChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (bar != null && level != null) bar.SetRatio(level.Progress01);
    }

    private void OnDestroy()
    {
        if (level != null) level.OnProgressChanged -= Refresh;
    }
}
