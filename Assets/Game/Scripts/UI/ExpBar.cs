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
    private const float MarginY = 56f;   // 체력바(72) 바로 아래
    private const float BarWidth = 300f;
    private const float BarHeight = 12f;
    private const int Border = 2;

    private static readonly Color FillColor = new Color(1f, 0.82f, 0.25f, 0.95f);

    private PlayerLevel level;
    private Image fill;

    public static ExpBar Create(Transform canvasRoot)
    {
        RectTransform panel = PixelUi.MakePanel(canvasRoot, "ExpBar", Border);
        panel.anchorMin = panel.anchorMax = Vector2.zero;
        panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(MarginX, MarginY);
        panel.sizeDelta = new Vector2(BarWidth, BarHeight);

        ExpBar bar = panel.gameObject.AddComponent<ExpBar>();

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(panel, false);
        bar.fill = fillGo.AddComponent<Image>();
        bar.fill.sprite = PrimitiveSprites.Square;
        bar.fill.color = FillColor;
        bar.fill.raycastTarget = false;
        bar.fill.type = Image.Type.Filled;
        bar.fill.fillMethod = Image.FillMethod.Horizontal;
        bar.fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        RectTransform fillRt = bar.fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(Border + 1, Border + 1);
        fillRt.offsetMax = new Vector2(-(Border + 1), -(Border + 1));

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
        if (fill != null && level != null) fill.fillAmount = Mathf.Clamp01(level.Progress01);
    }

    private void OnDestroy()
    {
        if (level != null) level.OnProgressChanged -= Refresh;
    }
}
