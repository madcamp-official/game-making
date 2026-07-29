using System.Collections;
using UnityEngine;

/// <summary>
/// 킹크랩의 가위치기 — 밀치기 전위. 플레이어 방향으로 넓은 부채꼴을 예고하고,
/// 가위를 닫으며(Strike) 범위 안의 플레이어를 공격 방향으로 강하게 밀어낸다.
///
/// 피해보다 넉백이 핵심이다. 아쿠스타의 레이저나 다른 적의 범위로 밀어 넣는
/// 역할이라 피해는 낮게, 미는 힘은 세게 둔다. 기절은 걸지 않고, 민 뒤에는
/// 잠시 멈춰 서서 반격할 틈을 준다.
/// </summary>
public class EnemyPincerAbility : EnemyAbility
{
    [Header("가위치기")]
    [Tooltip("부채꼴의 반지름. 예고와 판정이 같은 값을 쓴다.")]
    [SerializeField, Min(0.1f)] private float reach = 3f;
    [Tooltip("부채꼴의 전체 각도(도).")]
    [SerializeField, Range(10f, 180f)] private float sweepAngle = 110f;
    [SerializeField, Min(0.05f)] private float telegraph = 0.55f;
    [Tooltip("Strike 동작이 시작되고 실제로 닫히기까지의 시간.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.2f;
    [Tooltip("넉백이 역할이라 피해는 낮게 유지한다.")]
    [SerializeField, Min(0)] private int damage = 8;

    [Header("밀어내기")]
    [Tooltip("공격 방향으로 미는 속도. 감쇠하며 사라지는 임펄스다.")]
    [SerializeField, Min(0f)] private float knockbackSpeed = 15f;

    [Header("후딜")]
    [Tooltip("공격 후 멈춰 서서 반격을 허용하는 시간.")]
    [SerializeField, Min(0f)] private float recovery = 1f;

    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.5f);

    protected override IEnumerator Perform()
    {
        // 예고를 시작할 때 조준을 고정한다. 이후에는 따라가지 않아야 걸어서 피할 수 있다.
        Vector2 aim = DirectionToPlayer;
        float centerAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

        AttackTelegraph warning = AttackTelegraph.CreateSector(
            EffectRoot, transform.position, reach, centerAngle, sweepAngle, warningColor);
        warning.Pulse(telegraph);
        yield return new WaitForSeconds(telegraph);
        if (Health.IsDead) yield break;

        PlayAction("Strike", aim);
        yield return new WaitForSeconds(hitDelay);
        if (Health.IsDead) yield break;

        if (PlayerInSector(aim))
        {
            if (PlayerHealth != null && !PlayerHealth.IsInvincible && !PlayerHealth.IsDead)
                PlayerHealth.TakeDamage(damage);
            // 무적이어도 밀리는 건 밀린다 — 이 공격의 본체는 피해가 아니라 이동이다.
            PlayerCrowdControl cc = PlayerCrowdControl.Of(PlayerHealth);
            if (cc != null) cc.AddImpulse(aim * knockbackSpeed);
        }

        // 가위를 닫은 자세로 잠시 굳는다. 반격의 틈이다.
        float recoverEnd = Time.time + recovery;
        while (Time.time < recoverEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }

    private bool PlayerInSector(Vector2 aim)
    {
        if (Player == null) return false;
        Vector2 offset = PlayerPosition - (Vector2)transform.position;
        if (offset.magnitude > reach) return false;
        return Vector2.Angle(aim, offset) <= sweepAngle * 0.5f;
    }
}
