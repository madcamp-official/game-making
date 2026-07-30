using System.Collections;
using UnityEngine;

/// <summary>
/// 통로를 막는 구름 한 덩이. 싸움이 끝나지 않은 방에서 "이쪽으로는 못 나간다"를 몸으로
/// 보여 주는 것이 일이다.
///
/// 그림은 가로로 이어지는 띠에서 한 칸씩 밀린 프레임 열여섯 장이다. 순서대로 돌리면
/// 구름이 옆으로 흘러가는 것으로 보이고, 마지막 장에서 첫 장으로 돌아가도 이음매가 없다.
/// 열릴 때는 흐름을 멈추지 않고 그대로 옅어진다 — 갑자기 사라지면 사라진 자리가
/// 눈에 남고, 흘러 나가면 "길이 뚫렸다"로 읽힌다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class CorridorCloud : MonoBehaviour
{
    /// <summary>지형(−10, −5)보다 위, 캐릭터(10)보다 위 — 통로를 확실히 덮어야 한다.</summary>
    public const int SortingOrder = 20;

    [Tooltip("흘러가는 프레임. 가로로 한 칸씩 밀린 같은 띠여야 이음매가 없다.")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("초당 프레임 수. 한 장에 6px(0.25칸) 밀리므로 5면 초속 1.25칸이다.")]
    [SerializeField, Min(0.1f)] private float framesPerSecond = 5f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.45f;

    /// <summary>지금 지나갈 수 있는지.</summary>
    public bool IsOpen { get; private set; }

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D box;
    private Coroutine fading;
    private float phase;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
    }

    /// <summary>런타임에 구름을 세운다. 방마다 프리팹을 두지 않고 여기서 만든다.</summary>
    public static CorridorCloud Create(Transform parent, string name, Vector2 localPosition,
                                       Vector2 size, Sprite[] frames)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        sr.sortingOrder = SortingOrder;
        // 스프라이트 한 장이 2x2칸이라, 통로 입구 크기에 맞춰 늘린다.
        if (sr.sprite != null)
        {
            Vector2 native = sr.sprite.bounds.size;
            go.transform.localScale = new Vector3(
                native.x > 0f ? size.x / native.x : 1f,
                native.y > 0f ? size.y / native.y : 1f, 1f);
        }

        var box = go.AddComponent<BoxCollider2D>();
        // 콜라이더는 늘어난 스케일을 타므로 원본 크기 기준으로 1을 준다.
        box.size = Vector2.one;

        var cloud = go.AddComponent<CorridorCloud>();
        cloud.frames = frames;
        return cloud;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        // 열려서 안 보이는 동안에도 계속 흘려 둔다. 다시 닫힐 때 위상이 튀지 않는다.
        phase += Time.deltaTime * framesPerSecond;
        spriteRenderer.sprite = frames[(int)phase % frames.Length];
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

    /// <summary>흘러가며 옅어진다. 다 옅어지면 지나갈 수 있다.</summary>
    public void Open()
    {
        if (IsOpen) return;
        Restart(Fade(true));
    }

    /// <summary>흘러 들어오며 짙어진다. 짙어지기 전에 이미 막는다 — 뚫고 지나가면 안 된다.</summary>
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
