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
    private const float MarginY = 50f;   // 체력바(72) 바로 아래
    private const float BarWidth = 300f;
    private const float BarHeight = 14f;
    /// <summary>체력바의 "HP" 꼬리표 폭 + 사이 간격. 두 바의 왼쪽 끝을 맞춘다.</summary>
    private const float ChipOffset = 50f;

    /// <summary>
    /// 경험치는 푸른색이다 — <c>bars.png</c>의 "Exp. Point bar colors"에서 그대로 잰 값.
    /// 체력의 초록과 색으로 갈려서, 두 바가 나란히 있어도 어느 쪽인지 바로 읽힌다.
    /// </summary>
    private static readonly Color FillColor = new Color32(73, 146, 251, 255);

    private PlayerLevel level;
    private BarFill bar;

    public static ExpBar Create(Transform canvasRoot)
    {
        BarFill fill = BarFill.Create(canvasRoot, "ExpBar", FillColor);
        RectTransform rt = fill.Root;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(MarginX + ChipOffset, MarginY);
        rt.sizeDelta = new Vector2(BarWidth, BarHeight);

        ExpBar bar = fill.gameObject.AddComponent<ExpBar>();
        bar.bar = fill;
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
