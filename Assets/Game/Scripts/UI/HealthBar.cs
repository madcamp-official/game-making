using UnityEngine;

/// <summary>
/// Health 컴포넌트가 있는 오브젝트 머리 위에 표시되는 월드 스페이스 체력바.
///
/// 생김새는 좌하단 바(<see cref="BarFill"/>, <c>bars.png</c>에서 옮겼다)와 맞춘다 —
/// 어두운 윤곽, 흰 트랙, 그리고 위 한 줄이 짙은 두 톤 채움이다. 머리 위 바만 민무늬로
/// 두면 같은 게임 안에서 체력을 두 가지 문법으로 그리는 셈이 된다.
///
/// <b>색은 누구 것인지만 말한다 — 적은 빨강, 나는 초록.</b> 예전에는 남은 비율로
/// 초록에서 빨강으로 물들였는데, 그러면 위급할 때 내 바와 적 바가 같은 색이 되어
/// 화면의 빨간 바가 누구 것인지 알 수 없었다. 남은 양은 길이가 말한다.
///
/// 수치는 기본적으로 그리지 않는다. 적이 여럿 붙으면 머리 위 숫자가 서로 겹쳐
/// 화면이 어지럽고, 플레이어 체력은 좌하단 <see cref="PlayerHealthHud"/>에 크게 나온다.
/// </summary>
[RequireComponent(typeof(Health))]
public class HealthBar : MonoBehaviour
{
    [SerializeField] private float offsetY = 0.85f;
    [SerializeField] private float width = 0.9f;
    [SerializeField] private float height = 0.12f;
    [Tooltip("바 옆에 \"현재/최대\"를 함께 그린다. 디버그용이며 평소에는 꺼 둔다.")]
    [SerializeField] private bool showValue;

    /// <summary>내 바. 좌하단 <see cref="PlayerHealthHud"/>와 같은 초록이다.</summary>
    private static readonly Color PlayerFill = new Color(0.3f, 0.85f, 0.3f, 1f);
    /// <summary>적 바.</summary>
    private static readonly Color EnemyFill = new Color(0.85f, 0.15f, 0.15f, 1f);

    /// <summary>
    /// bars.png의 바 윤곽·트랙 색. 좌하단 바(<see cref="BarFill"/>)와 같은 값이다.
    ///
    /// 트랙은 흰색이 아니라 옅은 회색이다. 흰색으로 두면 아직 차지 않은 자리가 밝은 지형
    /// 위에서 유독 튀어, 남은 체력보다 <b>빈 자리</b>가 먼저 눈에 들어온다.
    /// </summary>
    private static readonly Color OutlineColor = new Color32(73, 73, 73, 255);
    private static readonly Color TrackColor = new Color32(186, 186, 186, 255);

    /// <summary>윤곽 두께(월드 단위). 바가 작아 한 픽셀 남짓이면 충분하다.</summary>
    private const float Outline = 0.035f;

    /// <summary>채움 위쪽 짙은 줄의 비율과 어두운 정도. <see cref="BarFill"/>과 같은 값이다.</summary>
    private const float ShadeFraction = 0.34f;
    private const float ShadeMultiplier = 0.62f;

    /// <summary>
    /// <b>왼쪽 끝</b>을 축으로 삼는 흰 사각형. 바의 네 조각이 모두 이것을 쓴다.
    ///
    /// 왜 전부 왼쪽 축인가: 이 씬의 카메라는 Pixel Perfect이고 Pixel Snapping이 켜져 있어
    /// (<c>m_GridSnapping: 1</c>, PPU 24) <b>렌더러마다 월드 자리를 픽셀 격자로 반올림한다</b>.
    /// 반올림이 조각마다 다른 쪽으로 떨어지면 조각들이 서로 한 픽셀씩 어긋난다.
    ///
    /// 어긋나지 않으려면 두 가지가 필요하다.
    /// <list type="number">
    /// <item>네 조각의 <b>왼쪽 끝이 같은 한 점</b>이어야 한다. 축이 가운데면 그 점이
    ///   중심 − 폭/2이라 폭에 따라 달라진다 — 트랙만 가운데 축으로 뒀을 때 여전히 밀린
    ///   이유가 이것이었다.</item>
    /// <item>조각들의 로컬 x 차이가 <b>픽셀의 정수배</b>여야 한다. 그러면 캐릭터가 어디에
    ///   서 있든 네 조각의 소수부가 같아서 반올림이 같은 방향으로 떨어진다.</item>
    /// </list>
    /// 그래서 크기와 자리를 모두 <see cref="SnapToPixel"/>로 픽셀에 맞춘다.
    /// </summary>
    private static Sprite whiteLeftSprite;

    /// <summary>
    /// PixelPerfectCamera의 Assets Pixels Per Unit(24)에 대응하는 한 픽셀의 월드 크기.
    /// 스냅 격자가 이 값이라, 바의 치수도 여기에 맞춰야 조각들이 함께 움직인다.
    /// </summary>
    private const float PixelSize = 1f / 24f;

    /// <summary>픽셀 격자에 맞춘 값. 프리팹에 굳어 있는 수치를 그대로 못 믿으므로 실행 중에 맞춘다.</summary>
    private static float SnapToPixel(float value) =>
        Mathf.Max(PixelSize, Mathf.Round(value / PixelSize) * PixelSize);

    private Health health;
    private Transform barRoot;
    private SpriteRenderer fillRenderer;
    private SpriteRenderer shadeRenderer;
    private TextMesh valueText;

    /// <summary>픽셀에 맞춘 실제 치수. 프리팹의 값을 반올림한 것이다.</summary>
    private float barWidth;
    private float barHeight;
    private float shadeHeight;

    private void Awake()
    {
        health = GetComponent<Health>();
        EnsureSprites();

        barWidth = SnapToPixel(width);
        barHeight = SnapToPixel(height);
        shadeHeight = SnapToPixel(barHeight * ShadeFraction);
        // 왼쪽 끝도 픽셀에 맞춘다. 네 조각이 여기서 함께 시작한다.
        float left = -SnapToPixel(barWidth * 0.5f);

        barRoot = new GameObject("HealthBar").transform;
        // worldPositionStays를 그대로 둔다(참). 적마다 몸 크기가 달라서(닥트리오 1.35배 등)
        // 그 배율이 바에 전해지면 바 크기가 적마다 달라진다. 이 값이 참이면 Unity가
        // 부모 배율을 상쇄하는 localScale을 넣어 주므로 바는 누구에게 붙어도 같은 크기다.
        barRoot.SetParent(transform);
        barRoot.localPosition = new Vector3(0f, SnapToPixel(offsetY), 0f);

        // 네 조각 모두 왼쪽 끝을 축으로 삼고 같은 x에서 시작한다. 윤곽만 한 픽셀 더 왼쪽이다.
        SpriteRenderer outline = CreatePart("Outline", barRoot, whiteLeftSprite, OutlineColor, 39);
        outline.transform.localPosition = new Vector3(left - PixelSize, 0f, 0f);
        outline.transform.localScale =
            new Vector3(barWidth + PixelSize * 2f, barHeight + PixelSize * 2f, 1f);

        // 흰 트랙 — 아직 차지 않은 자리다. 검정으로 두면 남은 양이 얼마인지 눈에 덜 띈다.
        SpriteRenderer track = CreatePart("Track", barRoot, whiteLeftSprite, TrackColor, 40);
        track.transform.localPosition = new Vector3(left, 0f, 0f);
        track.transform.localScale = new Vector3(barWidth, barHeight, 1f);

        // 프리팹마다 색을 따로 넣게 하면 적을 하나 추가할 때마다 빠뜨릴 자리가 생긴다.
        // 누구 몸에 붙었는지 보고 정한다.
        Color fillColor = GetComponent<PlayerController>() != null ? PlayerFill : EnemyFill;

        // 채움과 짙은 줄은 왼쪽 끝에 못박아 두고 폭만 줄인다. 자리는 앞으로 바뀌지 않는다.
        fillRenderer = CreatePart("Fill", barRoot, whiteLeftSprite, fillColor, 41);
        fillRenderer.transform.localPosition = new Vector3(left, 0f, 0f);

        // 위쪽 짙은 줄 — 좌하단 바와 같은 두 톤이다. 이게 없으면 바가 납작해 보인다.
        shadeRenderer = CreatePart("Shade", barRoot, whiteLeftSprite,
            new Color(fillColor.r * ShadeMultiplier, fillColor.g * ShadeMultiplier,
                      fillColor.b * ShadeMultiplier, fillColor.a), 42);
        shadeRenderer.transform.localPosition =
            new Vector3(left, SnapToPixel(barHeight * 0.5f) - shadeHeight * 0.5f, 0f);

        // 바 오른쪽에 "현재/최대" 수치 표시 (기본은 그리지 않는다)
        if (showValue)
        {
            GameObject textGo = new GameObject("Value");
            textGo.transform.SetParent(barRoot);
            textGo.transform.localPosition = new Vector3(width * 0.5f + 0.08f, 0f, 0f);
            valueText = textGo.AddComponent<TextMesh>();
            valueText.font = PixelUi.Font;
            // 비트맵 폰트에서 TextMesh는 fontSize를 무시하고 글리프 크기를 그대로 쓴다.
            // 실제 크기는 characterSize로만 정해진다 (글자 높이 = 글리프 픽셀 x characterSize / 10).
            valueText.fontSize = 0;
            valueText.characterSize = 0.16f;
            valueText.anchor = TextAnchor.MiddleLeft;
            valueText.color = Color.white;
            var textRenderer = textGo.GetComponent<MeshRenderer>();
            // UI/Default는 캔버스 밖에서 쓰기 부적절해 월드용 머티리얼을 따로 쓴다.
            textRenderer.material = PixelUi.WorldFontMaterial != null
                ? PixelUi.WorldFontMaterial : valueText.font.material;
            textRenderer.sortingOrder = 42;
        }

        health.OnHealthChanged += UpdateBar;
    }

    // Health.Awake의 체력 초기화가 끝난 뒤 첫 갱신을 해야 한다.
    private void Start()
    {
        UpdateBar(health.CurrentHealth, health.MaxHealth);
    }

    private static void EnsureSprites()
    {
        // 플레이 모드를 다시 들어오면 텍스처가 파괴되므로 널 검사로 다시 만든다.
        if (whiteLeftSprite != null) return;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteLeftSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
    }

    private static SpriteRenderer CreatePart(string name, Transform parent, Sprite sprite,
                                            Color color, int order)
    {
        GameObject go = new GameObject(name);
        // localPosition·localScale을 곧바로 정하므로 월드 자리를 지킬 필요가 없다.
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void UpdateBar(int current, int max)
    {
        // 색은 건드리지 않는다. 길이만 줄어든다 — 자리는 왼쪽 끝에 못박혀 있다.
        // 폭도 픽셀에 맞춰 끊는다. 소수 픽셀로 끝나면 오른쪽 끝이 반투명하게 번진다.
        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        float fillWidth = ratio <= 0f
            ? 0f
            : Mathf.Max(PixelSize, Mathf.Round(barWidth * ratio / PixelSize) * PixelSize);
        fillRenderer.transform.localScale = new Vector3(fillWidth, barHeight, 1f);
        shadeRenderer.transform.localScale = new Vector3(fillWidth, shadeHeight, 1f);
        if (valueText != null) valueText.text = current + "/" + max;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= UpdateBar;
    }
}
