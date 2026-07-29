using System.Collections;
using UnityEngine;

/// <summary>
/// 고지의 공격. 플레이어가 붙으면 발톱을 들어 올린 자세(StrikeReady)로 공격 방향을 길게
/// 예고하고, 전방을 넓게 할퀴는 <b>한 번의 강한</b> 공격(Strike)을 한다. 그 뒤 공 모양으로
/// 굳은 채(Guard) 뒤로 미끄러져 물러나, 그 자세 그대로 받는 피해를 크게 줄이고 버틴 뒤,
/// 몸을 펴며(Uncurl) 한동안 정지한다 — 방어가 풀리는 이 순간이 반격의 창이다.
///
/// 연타가 아니라 <b>한 방</b>인 이유: 예고가 길고 피해가 큰 단발이라야 "보고 피한다"가
/// 성립한다. 두 번 긁으면 첫 타를 피해도 둘째 타에 걸려, 읽어낸 값이 돌아오지 않는다.
///
/// Guard는 Attack 구르기가 가장 멀리 나아가 잠깐 멈춰 보이는 프레임 하나고,
/// Uncurl은 그 뒤의 남은 프레임들이다 — 물러날 때부터 버티는 내내 같은 공 모양이라
/// "지금 단단하다"가 끊기지 않고 읽힌다.
///
/// 조준은 예고 자세에서 고정하고 두 타 모두 같은 방향을 할퀸다 — 옆으로 비켜서는
/// 회피를 유도한다. 타격 프레임이 아니면 몸이 닿아 있어도 피해가 없다.
/// 말린 동안이 반격의 틈처럼 보이지만 실제로는 가장 단단한 순간이다. 굳은 고지를
/// 계속 때릴 것인지, 방어가 풀릴 때까지 다른 적부터 정리할 것인지를 고르게 한다.
/// </summary>
public class EnemyGuardAbility : EnemyAbility
{
    [Header("예고")]
    [Tooltip("발톱을 들어 올린 정지 자세. 비우면 예고 자세 없이 바로 할퀸다.")]
    [SerializeField] private string readyState = "StrikeReady";
    [Tooltip("예고 자세로 서 있는 시간. 이 시간 뒤에는 방향이 바뀌지 않는다. " +
             "한 방이 무거운 만큼 예고도 길게 준다.")]
    [SerializeField, Min(0f)] private float readyDuration = 0.6f;

    [Header("할퀴기 (단발)")]
    [SerializeField, Min(0)] private int strikeDamage = 22;
    [Tooltip("Strike 동작이 시작되고 실제로 때리기까지의 시간. 타격 프레임에 맞춘 값이다.")]
    [SerializeField, Min(0f)] private float strikeHitDelay = 0.18f;
    [Tooltip("할퀴기 동작의 전체 길이. 때린 뒤 자세를 끝까지 보여 주고 다음 단계로 넘어간다.")]
    [SerializeField, Min(0.1f)] private float strikeDuration = 0.5f;
    [Tooltip("몸 표면에서 더 뻗는 사거리.")]
    [SerializeField, Min(0f)] private float strikeReach = 1.2f;
    [Tooltip("할퀴기 판정 부채꼴의 전체 각도. '전방을 넓게'가 이 값이다.")]
    [SerializeField, Range(20f, 360f)] private float strikeSweepAngle = 180f;

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
    [Tooltip("몸을 편 뒤 무방비로 정지하는 시간. 방어가 풀린 값을 치르는 반격 창이다.")]
    [SerializeField, Min(0f)] private float recovery = 1.05f;

    protected override IEnumerator Perform()
    {
        // 1. 예고 — 발톱을 들고 노려본다. 자세가 곧 "이 방향을 할퀸다"다.
        float readyEnd = Time.time + readyDuration;
        while (Time.time < readyEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            if (!string.IsNullOrEmpty(readyState)) PlayAction(readyState, DirectionToPlayer);
            yield return null;
        }
        if (Health.IsDead) yield break;

        // 2. 예고한 방향을 한 번 크게 할퀸다. 타격 프레임에만 판정이 있다.
        Vector2 aim = DirectionToPlayer;
        ReplayAction("Strike", aim);
        {
            bool resolved = false;
            float elapsed = 0f;
            while (elapsed < strikeDuration && !Health.IsDead)
            {
                Body.linearVelocity = Vector2.zero;
                if (!resolved && elapsed >= strikeHitDelay)
                {
                    resolved = true;
                    if (PlayerWithinSector(aim, strikeReach, strikeSweepAngle) &&
                        PlayerHealth != null && !PlayerHealth.IsDead && !PlayerHealth.IsInvincible)
                        PlayerHealth.TakeDamage(strikeDamage);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        if (Health.IsDead) yield break;

        // 3. 공 모양으로 굳은 채 뒤로 미끄러져 물러난다. 물러날 때부터 이미 방어 자세라
        //    구르는 동작이 흐르지 않는다 — 흐르면 어느 순간부터 단단한지 읽히지 않는다.
        //    시선은 플레이어 쪽 그대로 — 등을 보이며 도망가는 게 아니라 방어 태세로 빠지는 것이다.
        PlayAction("Guard", aim);
        float retreatEnd = Time.time + retreatDuration;
        while (Time.time < retreatEnd && !Health.IsDead)
        {
            Body.linearVelocity = -aim * retreatSpeed;
            yield return null;
        }

        // 4. 같은 자세로 버틴다. 밀려도 제자리를 지킨다. 웅크린 동안 방향도 바꾸지 않는다.
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

        // 5. 남은 프레임으로 몸을 펴고, 무방비로 한동안 정지한다.
        PlayAction("Uncurl", aim);
        yield return new WaitForSeconds(uncurlDuration);
        StopAction();
        float pauseEnd = Time.time + recovery;
        while (Time.time < pauseEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }
}
