using System.Collections;
using UnityEngine;

/// <summary>
/// 고지의 공격. 플레이어가 붙으면 웅크려 받는 피해를 크게 줄인 채 버티다가,
/// 몸 주변에 짧은 범위 공격을 터뜨린다.
///
/// 웅크린 자세는 Attack 동작을 한 번 재생하고 마지막 프레임에서 굳혀 만든다
/// (클립이 반복 없음이라 재생이 끝나면 저절로 멈춘다). 웅크린 동안이 곧 예고다 —
/// 굳은 고지를 계속 때릴 것인지, 터지기 전에 물러날 것인지를 고르게 한다.
/// </summary>
public class EnemyGuardAbility : EnemyAbility
{
    [Header("웅크리기")]
    [Tooltip("웅크린 채 버티는 시간. 이 동안 범위 예고가 차오른다.")]
    [SerializeField, Min(0.1f)] private float guardDuration = 1.1f;
    [Tooltip("웅크린 동안 줄이는 피해 비율. 0.7이면 30%만 받는다.")]
    [SerializeField, Range(0f, 1f)] private float damageReduction = 0.7f;

    [Header("터뜨리기")]
    [SerializeField, Min(0f)] private float burstRadius = 1.7f;
    [SerializeField, Min(0)] private int burstDamage = 16;
    [SerializeField, Min(0f)] private float recovery = 0.7f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.4f);
    [SerializeField] private Color burstColor = new Color(0.95f, 0.75f, 0.3f, 0.7f);

    protected override IEnumerator Perform()
    {
        Vector2 aim = DirectionToPlayer;
        PlayAction("Attack", aim);
        Health.DamageTakenMultiplier = 1f - damageReduction;

        AttackTelegraph warning = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, burstRadius, warningColor);
        warning.Pulse(guardDuration);

        float end = Time.time + guardDuration;
        while (Time.time < end && !Health.IsDead)
        {
            // 웅크린 동안에는 밀려도 제자리를 지킨다. 방어형이 밀려나면 뒤를 지키는 뜻이 없다.
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }

        // 어떤 경로로 빠져나가든 배율은 반드시 되돌린다. 죽은 뒤에도 남으면 안 되는 값은 아니지만
        // (오브젝트가 곧 사라진다), 살아서 나가는 길이 여럿이라 여기 한 곳에서 처리한다.
        Health.DamageTakenMultiplier = 1f;
        if (Health.IsDead) yield break;

        // 터뜨리기. 판정은 한 번, 그린 원과 같은 반지름이다.
        AttackTelegraph burst = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, burstRadius, burstColor);
        burst.Hold(0.18f);

        if (PlayerHealth != null && !PlayerHealth.IsDead &&
            Vector2.Distance(transform.position, PlayerPosition) <= burstRadius + 0.3f)
            PlayerHealth.TakeDamage(burstDamage);

        StopAction();
        yield return new WaitForSeconds(recovery);
    }
}
