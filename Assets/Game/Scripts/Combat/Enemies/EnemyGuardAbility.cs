using System.Collections;
using UnityEngine;

/// <summary>
/// 고지의 공격. 플레이어가 붙으면 정면을 할퀴고(Strike), 공 모양으로 굳은 채(Guard) 뒤로
/// 미끄러져 물러나, 그 자세 그대로 받는 피해를 크게 줄이고 버틴 뒤, 몸을 펴며(Uncurl) 돌아온다.
///
/// Guard는 Attack 구르기가 가장 멀리 나아가 잠깐 멈춰 보이는 프레임 하나고,
/// Uncurl은 그 뒤의 남은 프레임들이다 — 물러날 때부터 버티는 내내 같은 공 모양이라
/// "지금 단단하다"가 끊기지 않고 읽힌다.
///
/// 할퀴기는 예고 없이 정면만 때린다 — 몸 바로 앞이라 자세만 보고 피해야 한다.
/// 말린 동안이 반격의 틈처럼 보이지만 실제로는 가장 단단한 순간이다. 굳은 고지를
/// 계속 때릴 것인지, 풀릴 때까지 다른 적부터 정리할 것인지를 고르게 한다.
/// </summary>
public class EnemyGuardAbility : EnemyAbility
{
    [Header("후려치기")]
    [SerializeField, Min(0)] private int strikeDamage = 14;
    [Tooltip("Strike 동작이 시작되고 실제로 때리기까지의 시간. 타격 프레임에 맞춘 값이다.")]
    [SerializeField, Min(0f)] private float strikeHitDelay = 0.18f;
    [Tooltip("때린 뒤 남은 동작이 끝나기를 기다리는 시간.")]
    [SerializeField, Min(0f)] private float strikeFollowThrough = 0.28f;
    [Tooltip("몸 앞 이 거리 지점을 중심으로 때린다.")]
    [SerializeField, Min(0f)] private float strikeReach = 1f;
    [Tooltip("타격 중심에서 이 반지름 안이면 맞는다.")]
    [SerializeField, Min(0f)] private float strikeRadius = 0.95f;

    [Header("말린 채 물러나기")]
    [SerializeField, Min(0f)] private float retreatSpeed = 4.5f;
    [SerializeField, Min(0f)] private float retreatDuration = 0.32f;

    [Header("방어")]
    [Tooltip("공 모양으로 굳어 버티는 시간.")]
    [SerializeField, Min(0.1f)] private float guardDuration = 1f;
    [Tooltip("말린 동안 줄이는 피해 비율. 0.7이면 30%만 받는다.")]
    [SerializeField, Range(0f, 1f)] private float damageReduction = 0.7f;
    [Tooltip("몸을 펴는 동작(Uncurl, 프레임 4개)의 길이.")]
    [SerializeField, Min(0f)] private float uncurlDuration = 0.14f;
    [SerializeField, Min(0f)] private float recovery = 0.5f;

    protected override IEnumerator Perform()
    {
        // 1. 후려치기 — 정면만, 예고 없이.
        Vector2 aim = DirectionToPlayer;
        PlayAction("Strike", aim);
        yield return new WaitForSeconds(strikeHitDelay);
        if (Health.IsDead) yield break;

        Vector2 hitCenter = (Vector2)transform.position + aim * strikeReach;
        if (PlayerHealth != null && !PlayerHealth.IsDead && !PlayerHealth.IsInvincible &&
            Vector2.Distance(hitCenter, PlayerPosition) <= strikeRadius)
            PlayerHealth.TakeDamage(strikeDamage);

        yield return new WaitForSeconds(strikeFollowThrough);
        if (Health.IsDead) yield break;

        // 2. 공 모양으로 굳은 채 뒤로 미끄러져 물러난다. 물러날 때부터 이미 방어 자세라
        //    구르는 동작이 흐르지 않는다 — 흐르면 어느 순간부터 단단한지 읽히지 않는다.
        //    시선은 플레이어 쪽 그대로 — 등을 보이며 도망가는 게 아니라 방어 태세로 빠지는 것이다.
        PlayAction("Guard", aim);
        float retreatEnd = Time.time + retreatDuration;
        while (Time.time < retreatEnd && !Health.IsDead)
        {
            Body.linearVelocity = -aim * retreatSpeed;
            yield return null;
        }

        // 3. 같은 자세로 버틴다. 밀려도 제자리를 지킨다.
        Health.DamageTakenMultiplier = 1f - damageReduction;
        float guardEnd = Time.time + guardDuration;
        while (Time.time < guardEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }

        // 살아서 나가는 길이 여럿이라 배율 복원은 여기 한 곳에서 처리한다.
        Health.DamageTakenMultiplier = 1f;
        if (Health.IsDead) yield break;

        // 4. 남은 프레임으로 몸을 펴고 제자리로 돌아간다.
        PlayAction("Uncurl", aim);
        yield return new WaitForSeconds(uncurlDuration);
        StopAction();
        yield return new WaitForSeconds(recovery);
    }
}
