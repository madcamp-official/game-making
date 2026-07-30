using System.Collections;
using UnityEngine;

/// <summary>
/// 통로를 막는 안개 둑. 싸움이 끝나지 않은 방에서 "이쪽으로는 못 나간다"를 몸으로
/// 보여 주는 것이 일이다.
///
/// 그림은 통로 입구부터 화면 밖까지 이어지는 정적인 안개 한 장이다 — PMD 붉은 구조대의
/// 엔딩 안개(fog.png)에서 덩이를 잘라 이어 붙였다(scratchpad의 bake_fogbank.py).
/// 예전에는 옆으로 흘러가는 프레임을 돌렸는데, 좁은 통로를 꽉 채운 것이 움직이면 벽이
/// 아니라 물살처럼 보여 뺐다 — 가만히 차 있어야 "막혔다"로 읽힌다. 방을 향한 면이
/// 둥글게 사그라드는 쪽이라, 왼쪽 통로에는 그림을 뒤집어 쓴다.
///
/// 열릴 때는 그 자리에서 옅어져 사라진다. 다 옅어진 뒤에야 지나갈 수 있다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class CorridorCloud : MonoBehaviour
{
    /// <summary>지형(−10, −5)보다 위, 캐릭터(10)보다 위 — 통로를 확실히 덮어야 한다.</summary>
    public const int SortingOrder = 20;

    [SerializeField, Min(0.05f)] private float fadeDuration = 0.55f;

    [Header("숨쉬기")]
    [Tooltip("막고 있는 동안 짙기가 오르내리는 폭. 0이면 가만히 서 있는다. " +
             "크게 잡으면 옅어진 순간에 통로 너머가 비쳐 '막혔다'가 흔들린다.")]
    [SerializeField, Range(0f, 0.4f)] private float breathDepth = 0.15f;
    [Tooltip("한 번 오르내리는 데 걸리는 시간의 범위. 구름마다 이 안에서 따로 뽑는다.")]
    [SerializeField] private Vector2 breathPeriod = new Vector2(2.6f, 4.4f);

    /// <summary>지금 지나갈 수 있는지.</summary>
    public bool IsOpen { get; private set; }

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D box;
    private Coroutine fading;

    // 숨쉬기 — 주기와 위상을 개체마다 따로 뽑는다.
    private float periodA, periodB, phaseA, phaseB;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();

        // 파장이 다른 두 물결을 겹친다. 하나만 쓰면 주기가 눈에 잡혀 기계처럼 뛰는데,
        // 서로 나누어떨어지지 않는 둘을 더하면 합의 주기가 아주 길어져 되풀이가 보이지 않는다.
        periodA = Random.Range(breathPeriod.x, breathPeriod.y);
        periodB = periodA * Random.Range(1.43f, 1.79f);
        phaseA = Random.Range(0f, Mathf.PI * 2f);
        phaseB = Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>
    /// 막고 있는 동안 짙기를 천천히 오르내린다. 통로를 꽉 채운 안개가 미동도 없으면
    /// 그려 붙인 벽처럼 보인다 — 짙기만 살짝 흔들려도 살아 있는 것으로 읽힌다.
    ///
    /// <b>자리도 크기도 건드리지 않는다.</b> 옆으로 흐르게 했다가 물살처럼 보여 뺐던 적이
    /// 있고, 크기를 흔들면 통로 가장자리에 틈이 생겼다 사라졌다 한다. 짙기만 만진다.
    ///
    /// 여닫는 중에는 손대지 않는다. 그쪽은 <see cref="Fade"/>가 알파를 쥐고 있고,
    /// 여기서 같이 쓰면 두 값이 서로를 덮어써 깜빡인다.
    /// </summary>
    private void Update()
    {
        if (IsOpen || fading != null || breathDepth <= 0f) return;

        float t = Time.time;
        float wave = Mathf.Sin(t / periodA * Mathf.PI * 2f + phaseA)
                   + Mathf.Sin(t / periodB * Mathf.PI * 2f + phaseB);
        // 두 물결의 합은 -2~2다. 0~1로 옮긴 뒤 짙기에서 그만큼 덜어 낸다.
        float amount = (wave + 2f) * 0.25f;

        Color c = spriteRenderer.color;
        c.a = 1f - breathDepth * amount;
        spriteRenderer.color = c;
    }

    /// <summary>런타임에 구름을 세운다. 방마다 프리팹을 두지 않고 여기서 만든다.</summary>
    /// <param name="size">그림을 늘릴 크기. 통로보다 넉넉해도 된다 — 이음매가 보이지 않아야 한다.</param>
    /// <param name="blockHeight">몸을 막는 높이. 통로 구멍의 높이여야 한다.</param>
    public static CorridorCloud Create(Transform parent, string name, Vector2 localPosition,
                                       Vector2 size, float blockHeight, Sprite sprite, bool faceRight)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = SortingOrder;
        // 그림의 둥근 얼굴은 텍스처 왼쪽에 있다. 얼굴이 오른쪽(방 쪽)을 봐야 하면 뒤집는다.
        sr.flipX = faceRight;
        // 통로 길이에 맞춰 늘린다. 원본이 딱 맞는 크기라 보통은 1배다.
        if (sprite != null)
        {
            Vector2 native = sprite.bounds.size;
            go.transform.localScale = new Vector3(
                native.x > 0f ? size.x / native.x : 1f,
                native.y > 0f ? size.y / native.y : 1f, 1f);
        }

        var box = go.AddComponent<BoxCollider2D>();
        // ⚠️ 막는 크기는 <b>통로 구멍과 똑같이</b> 준다 — 벽과 같은 판정이어야 한다.
        //
        // 두 번 어긋났던 자리다. 처음에는 1x1(원본 2x2칸을 스케일로 늘리던 시절의 값)이라
        // 한가운데 한 칸짜리 점이 됐고, 통로 높이가 2칸이라 위아래로 비켜 돌아갈 구멍이 났다.
        // 그 반동으로 그림 크기(높이 2.33칸)에 방 쪽으로 반 칸(reach)까지 더 내밀었더니,
        // 이번에는 벽 안쪽 면(±7)보다 0.3칸 안으로 들어와 <b>방 안에</b> 보이지 않는 턱이 생겼다 —
        // 오른쪽 벽에 붙어 오르내리기만 해도 아무것도 없는 곳에 걸렸다.
        //
        // 그림은 넉넉히 덮되(이음매가 보이면 안 된다), 막는 것은 뚫린 만큼만 막는다.
        Vector3 s = go.transform.localScale;
        box.size = new Vector2(size.x / s.x, blockHeight / s.y);
        box.offset = Vector2.zero;

        return go.AddComponent<CorridorCloud>();
    }

    /// <summary>연출 없이 상태만 맞춘다. 방에 들어서는 순간처럼 처음 값을 정할 때 쓴다.</summary>
    public void SetOpenImmediate(bool open)
    {
        if (fading != null) { StopCoroutine(fading); fading = null; }
        IsOpen = open;
        box.enabled = !open;
        Color c = spriteRenderer.color;
        c.a = open ? 0f : 1f;
        spriteRenderer.color = c;
        spriteRenderer.enabled = !open;
    }

    /// <summary>그 자리에서 옅어진다. 다 옅어지면 지나갈 수 있다.</summary>
    public void Open()
    {
        if (IsOpen) return;
        Restart(Fade(true));
    }

    /// <summary>다시 짙어진다. 짙어지기 전에 이미 막는다 — 뚫고 지나가면 안 된다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        Restart(Fade(false));
    }

    private void Restart(IEnumerator routine)
    {
        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(routine);
    }

    private IEnumerator Fade(bool opening)
    {
        IsOpen = opening;
        spriteRenderer.enabled = true;
        // 닫는 쪽은 그림이 다 짙어지기를 기다리지 않고 곧바로 막는다. 여는 쪽은 반대로
        // 다 옅어진 뒤에 푼다 — 어느 쪽이든 "보이는데 통과된다"가 없어야 한다.
        box.enabled = !opening;

        float from = spriteRenderer.color.a;
        float to = opening ? 0f : 1f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(from, to, elapsed / fadeDuration);
            spriteRenderer.color = c;
            yield return null;
        }

        Color end = spriteRenderer.color;
        end.a = to;
        spriteRenderer.color = end;
        spriteRenderer.enabled = !opening;
        fading = null;
    }
}
