using System.Collections;
using UnityEngine;

/// <summary>
/// 닥트리오의 공격. 평소에는 제자리에서 Idle로 서 있다가(기본 추적 AI를 꺼 둔다),
/// 땅속으로 잠수해 플레이어 발밑까지 파고들어 예고 후 솟아오르며 때린다.
///
/// 잠수 중에는 Walk 동작에 몸이 반투명해지고, 콜라이더가 꺼져 서로 부딪히지도
/// 맞지도 않는다 — 땅속에 있으니 당연하고, 대신 솟는 자리를 원으로 미리 알린다.
/// 솟아오르면 Idle로 다시 나타난다. 현재 위치와 파고드는 경로를 동시에 봐야 하는 적이다.
/// </summary>
public class EnemyBurrowAbility : EnemyAbility
{
    [Header("잠수")]
    [Tooltip("땅속 이동 속도. 플레이어(5)보다 확실히 빨라야 맞고 사라지는 것이 '도망'으로 읽힌다.")]
    [SerializeField, Min(0.5f)] private float diveSpeed = 9.5f;
    [Tooltip("파고드는 시간의 상한. 플레이어가 도망 다녀도 이 시간이 지나면 그 자리에서 솟는다.")]
    [SerializeField, Min(0.5f)] private float maxDiveTime = 2.4f;
    [Tooltip("플레이어와 이 거리 안이면 도착으로 본다.")]
    [SerializeField, Min(0f)] private float arriveDistance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float submergedAlpha = 0.45f;

    [Header("솟아오르기")]
    [Tooltip("도착해서 솟아오르기까지의 예고 시간. 이 시간이 곧 피할 시간이다.")]
    [SerializeField, Min(0.1f)] private float surfaceWindup = 0.55f;
    [SerializeField, Min(0f)] private float surfaceRadius = 1.25f;
    [SerializeField, Min(0)] private int damage = 16;
    [SerializeField, Min(0f)] private float recovery = 0.9f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.45f);
    [SerializeField] private Color burstColor = new Color(0.75f, 0.5f, 0.25f, 0.7f);

    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    protected override IEnumerator Perform()
    {
        // 잠수. 파고드는 동안에는 살아 있는 플레이어 위치를 계속 쫓는다 —
        // 솟는 자리는 어차피 도착한 뒤의 예고가 알려 준다.
        PlayAction("Walk", DirectionToPlayer);
        SetSubmerged(true);

        float deadline = Time.time + maxDiveTime;
        while (Time.time < deadline && !Health.IsDead)
        {
            Vector2 toPlayer = PlayerPosition - (Vector2)transform.position;
            if (toPlayer.magnitude <= arriveDistance) break;
            Body.linearVelocity = toPlayer.normalized * diveSpeed;
            PlayAction("Walk", toPlayer);
            yield return null;
        }

        Body.linearVelocity = Vector2.zero;
        if (Health.IsDead) { SetSubmerged(false); yield break; }

        // 예고. 잠수한 채 그 자리에서 차오른다.
        AttackTelegraph warning = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, surfaceRadius, warningColor);
        warning.Pulse(surfaceWindup);
        yield return new WaitForSeconds(surfaceWindup);

        // 솟아오르기. Idle로 다시 나타나며, 그린 원 안이면 맞는다.
        SetSubmerged(false);
        PlayAction("Idle", DirectionToPlayer);

        AttackTelegraph burst = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, surfaceRadius, burstColor);
        burst.Hold(0.18f);

        if (!Health.IsDead && PlayerHealth != null && !PlayerHealth.IsDead &&
            Vector2.Distance(transform.position, PlayerPosition) <= surfaceRadius + 0.3f)
            PlayerHealth.TakeDamage(damage);

        yield return new WaitForSeconds(recovery);
    }

    /// <summary>땅속 상태. 반투명해지고 콜라이더가 꺼진다 (부딪히지도, 맞지도 않는다).</summary>
    private void SetSubmerged(bool submerged)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = submerged ? submergedAlpha : 1f;
            spriteRenderer.color = c;
        }
        foreach (Collider2D col in GetComponents<Collider2D>())
            col.enabled = !submerged;
    }
}
