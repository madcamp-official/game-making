using UnityEngine;

/// <summary>
/// Health 컴포넌트가 있는 오브젝트 머리 위에 표시되는 월드 스페이스 체력바.
/// 배경(검정)과 채움(초록→빨강) 스프라이트를 런타임에 생성한다.
/// </summary>
[RequireComponent(typeof(Health))]
public class HealthBar : MonoBehaviour
{
    [SerializeField] private float offsetY = 0.85f;
    [SerializeField] private float width = 0.9f;
    [SerializeField] private float height = 0.12f;

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

        fillRenderer = CreatePart("Fill", fillPivot, Color.green, 41);
        fillRenderer.transform.localPosition = new Vector3(width * 0.5f, 0f, 0f);
        fillRenderer.transform.localScale = new Vector3(width, height * 0.7f, 1f);

        // 바 오른쪽에 "현재/최대" 수치 표시
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
        float ratio = max > 0 ? (float)current / max : 0f;
        fill.localScale = new Vector3(ratio, 1f, 1f);
        fillRenderer.color = Color.Lerp(Color.red, Color.green, ratio);
        if (valueText != null) valueText.text = current + "/" + max;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= UpdateBar;
    }
}
