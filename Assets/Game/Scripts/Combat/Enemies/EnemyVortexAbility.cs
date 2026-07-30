using System.Collections;
using UnityEngine;

/// <summary>
/// 강챙이의 소용돌이 — 흡인형 근접 적. 자신을 중심으로 넓은 소용돌이를 예고하고,
/// 범위 안의 플레이어를 일정 시간 끌어당긴(Charge) 뒤, 몸 주변에 원형 충격파(Idle)를
/// 터뜨려 피해와 함께 바깥으로 밀어낸다.
///
/// 흡인 중에는 피해가 없다. 당기는 힘은 플레이어 이동 속도보다 느려서, 반대로 계속
/// 걸으면 충격파 범위 밖으로 빠져나갈 수 있다. 킹크랩이 순간적으로 밀어낸다면
/// 강챙이는 시간을 들여 지속해서 당긴다.
/// </summary>
public class EnemyVortexAbility : EnemyAbility
{
    [Header("소용돌이(흡인)")]
    [Tooltip("이 반지름 안의 플레이어를 당긴다. 예고 원과 같은 크기다.")]
    [SerializeField, Min(0.1f)] private float vortexRadius = 4f;
    [SerializeField, Min(0.05f)] private float telegraph = 0.45f;
    [Tooltip("당기는 시간.")]
    [SerializeField, Min(0.1f)] private float pullDuration = 1.2f;
    [Tooltip("당기는 속도. 플레이어(5)보다 확실히 느려야 반대로 걸어 빠져나갈 수 있다.")]
    [SerializeField, Min(0f)] private float pullSpeed = 3.4f;
    [SerializeField] private Color vortexColor = new Color(0.25f, 0.55f, 0.95f, 0.35f);

    [Header("충격파")]
    [Tooltip("충격파 반지름. 흡인 반지름보다 훨씬 작아야 빠져나갈 보람이 있다.")]
    [SerializeField, Min(0.1f)] private float blastRadius = 2f;
    [SerializeField, Min(0)] private int blastDamage = 12;
    [Tooltip("충격파가 바깥으로 밀어내는 속도.")]
    [SerializeField, Min(0f)] private float blastKnockback = 13f;
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.5f);

    [Header("후딜")]
    [Tooltip("충격파 뒤의 후딜. 비교적 길게 둬서 확실한 반격 기회를 준다.")]
    [SerializeField, Min(0f)] private float recovery = 0.9f;

    protected override IEnumerator Perform()
    {
        Vector2 aim = DirectionToPlayer;

        // 1. 소용돌이 예고.
        AttackTelegraph vortexWarning = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, vortexRadius, vortexColor);
        vortexWarning.Pulse(telegraph);
        yield return new WaitForSeconds(telegraph);
        if (Health.IsDead) yield break;

        // 2. 흡인. 소용돌이 원은 옅게 유지하고, 진짜 위험한 충격파 범위를 위험색으로 함께
        //    보여 준다 — 빠져나가야 할 곳이 어디까지인지 당기는 내내 읽을 수 있어야 한다.
        PlayAction("Charge", aim);
        AttackTelegraph vortexField = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, vortexRadius, vortexColor);
        vortexField.Hold(pullDuration);
        AttackTelegraph blastWarning = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, blastRadius, warningColor);
        blastWarning.Pulse(pullDuration);

        PlayerCrowdControl cc = PlayerCrowdControl.Of(PlayerHealth);
        float pullEnd = Time.time + pullDuration;
        while (Time.time < pullEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            if (cc != null && Player != null)
            {
                Vector2 toSelf = (Vector2)transform.position - PlayerPosition;
                if (toSelf.magnitude <= vortexRadius && toSelf.sqrMagnitude > 0.04f)
                    cc.AddVelocity(toSelf.normalized * pullSpeed);
            }
            yield return new WaitForFixedUpdate();
        }
        if (Health.IsDead) yield break;

        // 3. 충격파. 피해를 주고 바깥으로 밀어낸다.
        PlayAction("Idle", aim);
        AttackTelegraph blast = AttackTelegraph.CreateRing(
            EffectRoot, transform.position, blastRadius, warningColor);
        blast.Expand(blastRadius * 0.4f, blastRadius, 0.18f);

        if (Player != null)
        {
            Vector2 offset = PlayerPosition - (Vector2)transform.position;
            if (offset.magnitude <= blastRadius)
            {
                if (PlayerHealth != null && !PlayerHealth.IsInvincible && !PlayerHealth.IsDead)
                    PlayerHealth.TakeDamage(blastDamage);
                Vector2 outward = offset.sqrMagnitude > 0.01f ? offset.normalized : aim;
                if (cc != null) cc.AddImpulse(outward * blastKnockback);
            }
        }

        // 4. 긴 후딜.
        float recoverEnd = Time.time + recovery;
        while (Time.time < recoverEnd && !Health.IsDead)
        {
            HoldPosition();
            yield return null;
        }
    }
}
