using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 왼쪽 아래에 붙는 플레이어 전용 체력바.
///
/// 적은 머리 위 <see cref="HealthBar"/>를 그대로 쓰고, 플레이어만 이쪽으로 옮겼다.
/// 자기 체력은 캐릭터를 보는 중에도 곁눈으로 읽어야 하는 정보라, 캐릭터에 붙어 다니면
/// 시선이 전투 상황과 겹쳐 오히려 안 보인다.
///
/// 생김새는 <c>Assets/Game/Art/UI/bars.png</c>(하트골드/소울실버 체력바)를 따른다 —
/// 왼쪽에 <b>"HP" 꼬리표</b>, 그 옆에 <b>어두운 윤곽을 두른 바</b>, 속은 흰 트랙이고
/// 남은 만큼만 색이 찬다. 채움은 위 한 줄이 짙어 띠가 납작해 보이지 않는다.
///
/// UI는 <see cref="UIManager"/>가 런타임에 만든다. 별도 프리팹이 필요 없다.
/// </summary>
public class PlayerHealthHud : MonoBehaviour
{
    // 아래에서부터 조작 안내(y 25) → 경험치 바(y 52) → 체력바 순으로 쌓는다.
    private const float MarginX = 30f;
    private const float MarginY = 82f;
    private const float BarWidth = 300f;
    private const float BarHeight = 26f;

    /// <summary>
    /// 내 체력은 남은 양과 상관없이 늘 초록이다. 예전에는 줄어들수록 빨강으로 물들었는데,
    /// 적 체력바도 같은 그라데이션이라 위급할 때 화면의 빨간 바가 내 것인지 적 것인지
    /// 구분이 되지 않았다. 색은 <b>누구 것인지</b>만 말하고, 남은 양은 길이가 말한다.
    /// </summary>
    private static readonly Color FillColor = new Color32(24, 195, 32, 255);

    /// <summary>
    /// "HP" 꼬리표. 색은 bars.png의 호박색을 조금 눌러 쓴다 — 글자를 <b>밝게</b> 둬야
    /// 폰트에 구워진 검은 윤곽이 제 몫을 하는데(<see cref="PmdUi.MakeText"/>), 원본
    /// 호박색(251,178,0)은 너무 밝아 흰 글자가 묻힌다.
    /// </summary>
    private static readonly Color ChipColor = new Color32(216, 138, 0, 255);
    private static readonly Color ChipInk = new Color32(255, 250, 236, 255);

    private Health health;
    private BarFill bar;
    private Text valueText;

    /// <summary>캔버스 아래에 체력바를 만들어 붙인다.</summary>
    public static PlayerHealthHud Create(Transform canvasRoot)
    {
        var go = new GameObject("PlayerHealthHud", typeof(RectTransform));
        RectTransform root = (RectTransform)go.transform;
        root.SetParent(canvasRoot, false);
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(MarginX, MarginY);
        root.sizeDelta = new Vector2(BarFill.BarOffsetX + BarWidth, BarHeight);

        PlayerHealthHud hud = go.AddComponent<PlayerHealthHud>();

        // "HP" 꼬리표 — 바 왼쪽에 붙고, 글자가 칸을 채운다.
        BarFill.MakeChip(root, "Chip", "HP", ChipColor, ChipInk, BarHeight);

        hud.bar = BarFill.Create(root, "Bar", FillColor);
        RectTransform barRt = hud.bar.Root;
        barRt.anchorMin = barRt.anchorMax = new Vector2(0f, 0.5f);
        barRt.pivot = new Vector2(0f, 0.5f);
        barRt.sizeDelta = new Vector2(BarWidth, BarHeight);
        barRt.anchoredPosition = new Vector2(BarFill.BarOffsetX, 0f);

        // 수치는 바 위에 겹쳐 얹는다. 폰트에 검은 윤곽이 구워져 있어 흰 트랙과 초록 채움을
        // 오가도 읽힌다 — Outline을 더 얹으면 그림자가 이중이 되어 오히려 뭉개진다.
        hud.valueText = PmdUi.MakeText(barRt, "Value", "", 24);
        hud.valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        PmdUi.Stretch(hud.valueText.rectTransform);

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
        if (bar != null) bar.SetRatio(ratio);
        if (valueText != null) valueText.text = current + " / " + max;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= Refresh;
    }
}
