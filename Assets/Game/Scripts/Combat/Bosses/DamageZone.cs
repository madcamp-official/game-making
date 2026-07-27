using UnityEngine;

/// <summary>
/// 바닥에 남아 지속 피해를 주는 원형 장판.
///
/// 물리 트리거 대신 중심 거리로 판정한다. 그려진 원이 곧 피해 범위라서 예고와 실제 판정이
/// 어긋나지 않고, 여러 장판이 겹쳐도 계산이 단순하다.
/// </summary>
public class DamageZone : MonoBehaviour
{
    /// <summary>경고 표시와 같은 높이. 지형보다 위, 캐릭터보다 아래.</summary>
    public const int SortingOrder = 1;

    private float radius;
    private int damage;
    private float tickInterval;
    private float expireTime;
    private float nextDamageTime;
    private Transform player;
    private Health playerHealth;
    private SpriteRenderer spriteRenderer;
    private float baseAlpha;

    public static DamageZone Spawn(Transform parent, Vector2 center, float radius, float duration,
                                   int damage, float tickInterval, Color color)
    {
        GameObject go = new GameObject("DamageZone");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = Vector3.one * (radius * 2f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Circle;
        sr.color = color;
        sr.sortingOrder = SortingOrder;

        DamageZone zone = go.AddComponent<DamageZone>();
        zone.spriteRenderer = sr;
        zone.baseAlpha = color.a;
        zone.radius = radius;
        zone.damage = damage;
        zone.tickInterval = tickInterval;
        zone.expireTime = Time.time + duration;
        return zone;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        player = pc.transform;
        playerHealth = pc.GetComponent<Health>();
    }

    private void Update()
    {
        if (Time.time >= expireTime)
        {
            Destroy(gameObject);
            return;
        }

        // 사라지기 직전 0.6초 동안 옅어져서 곧 없어진다는 걸 알린다.
        float remaining = expireTime - Time.time;
        if (remaining < 0.6f && spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = baseAlpha * (remaining / 0.6f);
            spriteRenderer.color = c;
        }

        if (player == null || playerHealth == null || playerHealth.IsDead) return;
        if (Time.time < nextDamageTime) return;

        float distance = Vector2.Distance(player.position, transform.position);
        if (distance > radius) return;

        // 다른 장판이나 탄에 맞은 직후라면 이번 프레임은 넘긴다. 무적이 풀리는 순간
        // 바로 다시 시도하므로, 계속 서 있으면 결국 피해를 입는다.
        if (playerHealth.IsInvincible) return;

        playerHealth.TakeDamage(damage);
        nextDamageTime = Time.time + tickInterval;
    }
}
