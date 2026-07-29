using UnityEngine;

/// <summary>
/// Health 컴포넌트가 있는 오브젝트 머리 위에 표시되는 월드 스페이스 체력바.
/// 배경(검정)과 채움 스프라이트를 런타임에 생성한다.
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

    private static Sprite whiteSprite;

    private Health health;
    private Transform barRoot;
    private Transform fill;
    private SpriteRenderer fillRenderer;
    private TextMesh valueText;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        barRoot = new GameObject("HealthBar").transform;
        barRoot.SetParent(transform);
        barRoot.localPosition = new Vector3(0f, offsetY, 0f);

        SpriteRenderer bg = CreatePart("BG", barRoot, new Color(0.1f, 0.1f, 0.1f, 0.9f), 40);
        bg.transform.localScale = new Vector3(width, height, 1f);

        // 채움은 왼쪽 기준으로 줄어들도록 부모를 왼쪽 끝에 둔다.
        Transform fillPivot = new GameObject("FillPivot").transform;
        fillPivot.SetParent(barRoot);
        fillPivot.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
        fill = fillPivot;

        // 프리팹마다 색을 따로 넣게 하면 적을 하나 추가할 때마다 빠뜨릴 자리가 생긴다.
        // 누구 몸에 붙었는지 보고 정한다.
        Color fillColor = GetComponent<PlayerController>() != null ? PlayerFill : EnemyFill;
        fillRenderer = CreatePart("Fill", fillPivot, fillColor, 41);
        fillRenderer.transform.localPosition = new Vector3(width * 0.5f, 0f, 0f);
        fillRenderer.transform.localScale = new Vector3(width, height * 0.7f, 1f);

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

    private SpriteRenderer CreatePart(string name, Transform parent, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void UpdateBar(int current, int max)
    {
        // 색은 건드리지 않는다. 길이만 줄어든다.
        float ratio = max > 0 ? (float)current / max : 0f;
        fill.localScale = new Vector3(ratio, 1f, 1f);
        if (valueText != null) valueText.text = current + "/" + max;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= UpdateBar;
    }
}
