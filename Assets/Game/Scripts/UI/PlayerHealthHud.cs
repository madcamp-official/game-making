using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 왼쪽 아래에 붙는 플레이어 전용 체력바.
///
/// 적은 머리 위 <see cref="HealthBar"/>를 그대로 쓰고, 플레이어만 이쪽으로 옮겼다.
/// 자기 체력은 캐릭터를 보는 중에도 곁눈으로 읽어야 하는 정보라, 캐릭터에 붙어 다니면
/// 시선이 전투 상황과 겹쳐 오히려 안 보인다.
///
/// UI는 <see cref="UIManager"/>가 런타임에 만든다. 별도 프리팹이 필요 없다.
/// </summary>
public class PlayerHealthHud : MonoBehaviour
{
    // 아래에서부터 조작 안내(y 15) → 경험치 바(y 56) → 체력바 순으로 쌓는다.
    private const float MarginX = 30f;
    private const float MarginY = 72f;
    private const float BarWidth = 300f;
    private const float BarHeight = 36f;
    private const int Border = 2;
    private const int FontSize = 24;   // PMD 폰트라 12의 배수여야 한다

    /// <summary>
    /// 내 체력은 남은 양과 상관없이 늘 초록이다. 예전에는 줄어들수록 빨강으로 물들었는데,
    /// 적 체력바도 같은 그라데이션이라 위급할 때 화면의 빨간 바가 내 것인지 적 것인지
    /// 구분이 되지 않았다. 색은 <b>누구 것인지</b>만 말하고, 남은 양은 길이가 말한다.
    /// </summary>
    private static readonly Color FillColor = new Color(0.3f, 0.85f, 0.3f, 1f);

    private Health health;
    private Image fill;
    private Text valueText;

    /// <summary>캔버스 아래에 체력바를 만들어 붙인다.</summary>
    public static PlayerHealthHud Create(Transform canvasRoot)
    {
        RectTransform panel = PixelUi.MakePanel(canvasRoot, "PlayerHealthHud", Border);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.zero;
        panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(MarginX, MarginY);
        panel.sizeDelta = new Vector2(BarWidth, BarHeight);

        PlayerHealthHud hud = panel.gameObject.AddComponent<PlayerHealthHud>();

        // 채움은 Filled 이미지로 왼쪽부터 줄어들게 한다. 크기를 직접 건드리는 것보다
        // 레이아웃이 단순하고, 테두리 안쪽에 정확히 맞춰 둘 수 있다.
        GameObject fillGo = new GameObject("Bar");
        fillGo.transform.SetParent(panel, false);
        hud.fill = fillGo.AddComponent<Image>();
        hud.fill.sprite = PrimitiveSprites.Square;
        hud.fill.type = Image.Type.Filled;
        hud.fill.fillMethod = Image.FillMethod.Horizontal;
        hud.fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hud.fill.raycastTarget = false;
        RectTransform fillRt = hud.fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(Border + 2, Border + 2);
        fillRt.offsetMax = new Vector2(-(Border + 2), -(Border + 2));

        hud.valueText = PixelUi.MakeText(panel, "Value", FontSize, Color.white, TextAnchor.MiddleCenter);
        hud.valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform textRt = hud.valueText.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        // 숫자는 항상 채움 위에 그린다.
        textRt.SetAsLastSibling();

        return hud;
    }

    private void Start()
    {
        Bind();
    }

    private void Update()
    {
        // 플레이어가 아직 없거나 교체됐으면 다시 잡는다. 방 이동은 플레이어를 그대로 두지만,
        // 재시작 등으로 새 플레이어가 생기면 구독이 끊긴 채 남는다.
        if (health == null) Bind();
    }

    private void Bind()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Health found = player.GetComponent<Health>();
        if (found == null || found == health) return;

        if (health != null) health.OnHealthChanged -= Refresh;
        health = found;
        health.OnHealthChanged += Refresh;
        Refresh(health.CurrentHealth, health.MaxHealth);
    }

    private void Refresh(int current, int max)
    {
        float ratio = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
        if (fill != null)
        {
            fill.fillAmount = ratio;
            fill.color = FillColor;
        }
        if (valueText != null) valueText.text = current + " / " + max;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= Refresh;
    }
}
