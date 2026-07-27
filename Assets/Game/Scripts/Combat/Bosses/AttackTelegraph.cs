using System.Collections;
using UnityEngine;

/// <summary>
/// 공격 예고 표시. 실제 피해 범위와 같은 크기로 그려서, 어디가 위험한지 미리 보여준다.
///
/// 지형보다 위·캐릭터보다 아래에 그린다(<see cref="SortingOrder"/>). 예고 시간 동안
/// 투명도를 깜빡여 눈에 띄게 하고, 시간이 끝나면 스스로 사라진다.
/// </summary>
public class AttackTelegraph : MonoBehaviour
{
    /// <summary>지형(-10, -5)보다 위, 캐릭터(5, 10)보다 아래.</summary>
    public const int SortingOrder = 0;

    private SpriteRenderer spriteRenderer;
    private float baseAlpha;

    /// <summary>방향성 공격의 발사선. 실제 발사 방향과 반드시 같은 각도로 그린다.</summary>
    public static AttackTelegraph CreateLine(Transform parent, Vector2 origin, Vector2 direction,
                                            float length, float width, Color color)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        AttackTelegraph telegraph = Create(parent, origin + dir * (length * 0.5f), PrimitiveSprites.Square, color);
        telegraph.transform.localScale = new Vector3(length, width, 1f);
        telegraph.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
        return telegraph;
    }

    /// <summary>장판 예고. <paramref name="radius"/>는 실제 장판 반지름과 같아야 한다.</summary>
    public static AttackTelegraph CreateCircle(Transform parent, Vector2 center, float radius, Color color)
    {
        AttackTelegraph telegraph = Create(parent, center, PrimitiveSprites.Circle, color);
        telegraph.transform.localScale = Vector3.one * (radius * 2f);
        return telegraph;
    }

    /// <summary>사방으로 퍼지는 공격의 예고, 또는 페이즈 전환 파동.</summary>
    public static AttackTelegraph CreateRing(Transform parent, Vector2 center, float radius, Color color)
    {
        AttackTelegraph telegraph = Create(parent, center, PrimitiveSprites.Ring, color);
        telegraph.transform.localScale = Vector3.one * (radius * 2f);
        return telegraph;
    }

    private static AttackTelegraph Create(Transform parent, Vector2 position, Sprite sprite, Color color)
    {
        GameObject go = new GameObject("AttackTelegraph");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = SortingOrder;

        AttackTelegraph telegraph = go.AddComponent<AttackTelegraph>();
        telegraph.spriteRenderer = sr;
        telegraph.baseAlpha = color.a;
        return telegraph;
    }

    /// <summary>예고 시간 동안 깜빡인 뒤 사라진다. 공격을 실제로 실행하는 쪽에서 기다린다.</summary>
    public void Pulse(float duration)
    {
        StartCoroutine(PulseRoutine(duration));
    }

    /// <summary>반지름을 넓히며 사라지는 파동. 피해는 없다.</summary>
    public void Expand(float fromRadius, float toRadius, float duration)
    {
        StartCoroutine(ExpandRoutine(fromRadius, toRadius, duration));
    }

    private IEnumerator PulseRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 예고가 끝나갈수록 빨리 깜빡여 임박했다는 걸 알린다.
            float speed = Mathf.Lerp(4f, 12f, elapsed / Mathf.Max(0.01f, duration));
            float wave = 0.65f + 0.35f * Mathf.Sin(elapsed * speed);
            SetAlpha(baseAlpha * wave);
            yield return null;
        }
        Destroy(gameObject);
    }

    private IEnumerator ExpandRoutine(float fromRadius, float toRadius, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            transform.localScale = Vector3.one * (Mathf.Lerp(fromRadius, toRadius, t) * 2f);
            SetAlpha(baseAlpha * (1f - t));
            yield return null;
        }
        Destroy(gameObject);
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer == null) return;
        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }
}
