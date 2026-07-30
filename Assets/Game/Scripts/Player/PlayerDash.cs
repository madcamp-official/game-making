using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 돌진의 공용 실행부. 파도타기와 로켓박치기가 수치만 달리해 함께 쓴다.
///
/// 이동은 <b>속도로만</b> 민다 — 위치를 직접 쓰면 벽을 뚫는다. 적 CC와 같은 원칙이다
/// (<see cref="PlayerCrowdControl"/>). 벽은 물리가 막아 주고, 여기서는 "막혀서 더 못
/// 나아간다"를 눈치채면 돌진을 끝낸다. 스치는 적은 하나당 한 번만 때린다.
///
/// <see cref="PlayerController"/>가 FixedUpdate에서 속도를 확정한 뒤에 덮어써야 하므로
/// 실행 순서를 그보다 뒤로 미룬다 (CC의 60보다도 뒤 — 돌진 중에는 밀리는 힘도 무시한다).
///
/// 무적(로켓박치기)은 <see cref="Health.BeginInvulnerability"/> 잠금으로 건다.
/// <b>어떤 길로 끝나든 반드시 해제된다</b> — 끝나는 길이 시간 만료·벽·사망·비활성화로
/// 갈라져 있어, 해제를 <see cref="End"/> 한 곳에 모으고 모든 길이 거기를 지나게 했다.
/// (사망 시에는 <see cref="Health"/>가 잠금을 통째로 비우므로 여기서 또 풀면 음수가
/// 될 것 같지만, EndInvulnerability가 0 아래로 내려가지 않게 막는다.)
/// </summary>
[DefaultExecutionOrder(70)]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDash : MonoBehaviour
{
    [Tooltip("돌진 중 적을 때리는 판정 반지름. 몸 크기보다 살짝 크게 잡아 스친 적도 맞는다.")]
    [SerializeField, Min(0f)] private float hitRadius = 0.7f;

    [Tooltip("한 물리 프레임의 실제 이동이 기대치의 이 비율에 못 미치면 벽에 막힌 것으로 본다.")]
    [SerializeField, Range(0.05f, 0.9f)] private float blockedFraction = 0.35f;

    private static readonly Collider2D[] hitBuffer = new Collider2D[16];
    private static readonly ContactFilter2D noFilter = ContactFilter2D.noFilter;

    private Rigidbody2D body;
    private Health health;
    private readonly List<Health> struck = new List<Health>(8);

    private Vector2 direction;
    private float speed;
    private float endTime;
    private int damage;
    private float knockback;
    private bool invulnerable;
    private Vector2 lastPosition;
    private bool moved; // 첫 프레임에는 막힘 판정을 하지 않는다 (아직 기대 이동이 없다)
    private int blockedFrames;

    public bool IsDashing { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
    }

    /// <summary>
    /// 돌진을 시작한다. 피해·넉백은 시전 순간의 강화·배율이 반영된 값을 받는다.
    /// 이미 돌진 중이면 무시한다 — 시전 쪽 쿨타임이 막아 주지만, 겹치면 무적 잠금이 꼬인다.
    /// </summary>
    public void Begin(Vector2 dir, float dashSpeed, float duration, int hitDamage,
                      float knockbackForce, bool grantInvulnerability)
    {
        if (IsDashing || duration <= 0f) return;
        direction = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
        speed = dashSpeed;
        endTime = Time.time + duration;
        damage = hitDamage;
        knockback = knockbackForce;
        struck.Clear();
        moved = false;
        blockedFrames = 0;
        lastPosition = body.position;

        invulnerable = grantInvulnerability && health != null && !health.IsDead;
        if (invulnerable) health.BeginInvulnerability();
        IsDashing = true;
    }

    /// <summary>돌진을 끝낸다. 무적 해제가 이 안에 있으므로 모든 종료가 여기를 지나야 한다.</summary>
    public void End()
    {
        if (!IsDashing) return;
        IsDashing = false;
        if (invulnerable)
        {
            invulnerable = false;
            if (health != null) health.EndInvulnerability();
        }
        body.linearVelocity = Vector2.zero;
    }

    private void OnDisable() => End();

    private void FixedUpdate()
    {
        if (!IsDashing) return;

        // 사망·시간 만료 — 무적이 남지 않게 End를 지난다.
        if ((health != null && health.IsDead) || Time.time >= endTime) { End(); return; }

        // 벽 판정: 지난 프레임에 기대만큼 나아가지 못했으면 막힌 것이다.
        // 두 프레임 연속일 때만 끝낸다 — 적과 부딪히는 순간에도 한 프레임쯤 느려지는데,
        // 적은 넉백으로 곧 밀려나므로(벽은 밀려나지 않는다) 한 프레임으로 접으면 오판한다.
        if (moved)
        {
            float expected = speed * Time.fixedDeltaTime;
            bool blocked = Vector2.Distance(body.position, lastPosition) < expected * blockedFraction;
            blockedFrames = blocked ? blockedFrames + 1 : 0;
            if (blockedFrames >= 2) { End(); return; }
        }
        lastPosition = body.position;
        moved = true;

        body.linearVelocity = direction * speed;
        StrikeOverlapping();
    }

    /// <summary>지나가는 길에 겹친 적을 때린다. 적 하나는 돌진 한 번에 한 번만 맞는다.</summary>
    private void StrikeOverlapping()
    {
        int count = Physics2D.OverlapCircle(body.position, hitRadius, noFilter, hitBuffer);
        bool hitAny = false;
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = hitBuffer[i].GetComponentInParent<EnemyController>();
            if (enemy == null) continue;
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (struck.Contains(enemyHealth)) continue;
            struck.Add(enemyHealth);

            enemyHealth.TakeDamage(damage);
            enemy.ApplyKnockback(direction, knockback);
            PlayerRelicEffects.ReportDamageDealt(damage);
            hitAny = true;
        }
        if (hitAny) GameAudio.PlayPlayerHit();
    }
}
