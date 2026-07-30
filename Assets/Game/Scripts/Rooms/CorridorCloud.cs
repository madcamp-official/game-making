using System.Collections;
using UnityEngine;

/// <summary>
/// 통로를 막는 뭉게구름 둑. 싸움이 끝나지 않은 방에서 "이쪽으로는 못 나간다"를 몸으로
/// 보여 주는 것이 일이다.
///
/// 그림은 통로 입구부터 화면 밖까지 이어지는 정적인 구름 한 장이다(scratchpad의
/// bake_cloudbank.py가 굽는다). 예전에는 옆으로 흘러가는 프레임을 돌렸는데, 좁은 통로를
/// 꽉 채운 구름이 움직이면 벽이 아니라 물살처럼 보여 뺐다 — 가만히 차 있어야 "막혔다"로
/// 읽힌다. 방을 향한 면이 둥근 덩이들이라, 왼쪽 통로에는 그림을 뒤집어 쓴다.
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

    /// <summary>지금 지나갈 수 있는지.</summary>
    public bool IsOpen { get; private set; }

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D box;
    private Coroutine fading;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
    }

    /// <summary>런타임에 구름을 세운다. 방마다 프리팹을 두지 않고 여기서 만든다.</summary>
    public static CorridorCloud Create(Transform parent, string name, Vector2 localPosition,
                                       Vector2 size, Sprite sprite, bool faceRight)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = SortingOrder;
        // 그림의 둥근 얼굴이 왼쪽(방 쪽)을 본다. 왼쪽 통로는 뒤집어 오른쪽을 보게 한다.
        sr.flipX = !faceRight;
        // 통로 길이에 맞춰 늘린다. 원본이 딱 맞는 크기라 보통은 1배다.
        if (sprite != null)
        {
            Vector2 native = sprite.bounds.size;
            go.transform.localScale = new Vector3(
                native.x > 0f ? size.x / native.x : 1f,
                native.y > 0f ? size.y / native.y : 1f, 1f);
        }

        var box = go.AddComponent<BoxCollider2D>();
        // 콜라이더는 늘어난 스케일을 타므로 원본 크기 기준으로 1을 준다.
        box.size = Vector2.one;

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
