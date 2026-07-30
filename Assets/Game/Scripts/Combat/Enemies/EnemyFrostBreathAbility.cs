using System.Collections;
using UnityEngine;

/// <summary>
/// 쥬래곤의 냉기 분사 — 감속 지원 적. 방 저편에서부터 가느다란 부채꼴을 겨누고,
/// 예고하는 동안에도 뿜는 동안에도 <b>플레이어를 따라 돌린다</b>(Charge).
///
/// 직접 피해는 없다. 냉기에 노출된 시간이 쌓일수록 감속이 강해질 뿐이다.
/// 노출을 끊으면 쌓인 시간이 서서히 줄어, 스치기만 한 플레이어는 곧 원래 발이 된다.
///
/// <b>빙결은 뺐다.</b> 몸이 통째로 멈추는 것은 피해가 없는 기술이 치를 값이 아니었다 —
/// 얼어붙은 0.5초에 다른 적의 예고가 겹치면 읽고도 못 피하는데, 정작 이 기술 자체는
/// 그 상황을 만든 책임을 지지 않는다. 이 적이 하는 일은 <b>느리게 만드는 것</b> 하나다.
///
/// <b>대신 조준이 쫓아온다.</b> 예전에는 시전할 때 방향을 한 번 정하고 끝이라, 옆으로
/// 두 걸음이면 그만이었다 — 있으나 마나 한 적이었다. 이제 나인테일처럼 회전 속도에
/// 상한을 두고 따라 돈다(<see cref="turnSpeed"/>). 다만 나인테일은 뿜기 시작할 때
/// 방향을 잠그는데 여기는 <b>잠그지 않는다.</b> 피해가 없기 때문에 붙잡혀도 죽지 않고,
/// 그래야 "느려지는 것은 정해져 있고 언제 벗어나느냐만 남는다"가 성립한다.
///
/// 회전 속도가 곧 <b>거리에 따른 난이도</b>다. 옆으로 도는 플레이어의 각속도는
/// 속도 ÷ 거리이므로, 거리 <c>5 / turnSpeed(라디안)</c>보다 멀면 아무리 돌아도 각이
/// 벌어지지 않는다 — 45°/초에서는 <b>약 6.4칸</b>이 그 경계다. 멀리서 겨눠질 때는
/// 피할 수 없고, 붙으면 쉽게 떨어낸다. 이 적에게 다가가는 것이 곧 답이다.
/// </summary>
public class EnemyFrostBreathAbility : EnemyAbility
{
    [Header("냉기 부채꼴")]
    [Tooltip("부채꼴의 반지름. 시전 거리(range)보다 넉넉히 넓어야 가장자리에서 시작한 " +
             "분사가 헛돌지 않는다.")]
    [SerializeField, Min(0.1f)] private float reach = 11.5f;
    [Tooltip("전체 각도. 사거리를 방 하나 길이로 늘렸으므로 좁혀야 한다 — 같은 각도를 두면 " +
             "부채꼴 하나가 방의 절반을 덮는다.")]
    [SerializeField, Range(10f, 180f)] private float sweepAngle = 22f;
    [Tooltip("예고 중·분사 중 조준이 도는 최대 속도(초당 각도). 이 값이 '어느 거리부터 " +
             "피할 수 없는가'를 정한다 — 경계 거리 = 플레이어 속도(5) ÷ 이 값(라디안).")]
    [SerializeField, Min(0f)] private float turnSpeed = 45f;
    [SerializeField, Min(0.05f)] private float telegraph = 0.45f;
    [Tooltip("냉기를 뿜는 시간. 이 동안에도 조준은 계속 따라 돈다.")]
    [SerializeField, Min(0.1f)] private float breathDuration = 1.5f;
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.45f);
    [SerializeField] private Color frostColor = new Color(0.6f, 0.85f, 1f, 0.4f);

    [Header("감속")]
    [Tooltip("노출이 이만큼 쌓이면 감속이 최대가 된다(초).")]
    [SerializeField, Min(0.1f)] private float maxSlowExposure = 0.8f;
    [Tooltip("최대로 쌓였을 때 남는 이동 속도 비율.")]
    [SerializeField, Range(0.05f, 1f)] private float maxSlowFactor = 0.45f;
    [Tooltip("냉기 밖에서 쌓인 노출이 초당 줄어드는 양.")]
    [SerializeField, Min(0f)] private float exposureDecay = 1.6f;

    [Header("후딜")]
    [SerializeField, Min(0f)] private float recovery = 0.7f;

    /// <summary>쌓인 노출 시간. 시전이 끝나도 이어져, 연속 시전이 이어 붙는다.</summary>
    private float exposure;

    /// <summary>지금 겨누고 있는 각도(도). 예고와 분사가 한 값을 이어 쓴다.</summary>
    private float aimAngle;

    protected override IEnumerator Perform()
    {
        aimAngle = AngleOf(DirectionToPlayer);

        // 1. 예고 — 부채꼴이 플레이어를 따라 돈다.
        AttackTelegraph warning = AttackTelegraph.CreateSector(
            EffectRoot, transform.position, reach, aimAngle, sweepAngle, warningColor);
        warning.Pulse(telegraph);
        yield return TrackWhile(telegraph, warning, null);
        if (Health.IsDead) yield break;

        // 2. 분사 — 방향을 잠그지 않는다. 실제 판정 범위를 냉기색으로 계속 보여 준다.
        AttackTelegraph frost = AttackTelegraph.CreateSector(
            EffectRoot, transform.position, reach, aimAngle, sweepAngle, frostColor);
        frost.Hold(breathDuration);

        yield return TrackWhile(breathDuration, frost, PlayerCrowdControl.Of(PlayerHealth));
        if (Health.IsDead) yield break;

        yield return new WaitForSeconds(recovery);
    }

    /// <summary>
    /// <paramref name="seconds"/> 동안 제자리에서 조준을 따라 돌린다.
    /// <paramref name="cc"/>가 있으면 그동안 부채꼴 안의 플레이어에게 냉기를 먹인다.
    /// </summary>
    private IEnumerator TrackWhile(float seconds, AttackTelegraph shape, PlayerCrowdControl cc)
    {
        float end = Time.time + seconds;
        while (Time.time < end && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;

            // 곧바로 겨누지 않고 정해진 속도만큼만 돌린다. 이 상한이 없으면 부채꼴이 몸에
            // 붙어 다녀서 거리와 무관하게 영영 벗어날 수 없다.
            aimAngle = turnSpeed > 0f
                ? Mathf.MoveTowardsAngle(aimAngle, AngleOf(DirectionToPlayer), turnSpeed * Time.deltaTime)
                : AngleOf(DirectionToPlayer);
            Vector2 aim = CurrentAim;

            // 그린 것이 곧 판정이다 — 몸이 밀려도 그림이 따라오도록 자리까지 매번 맞춘다.
            if (shape != null)
            {
                shape.transform.position = transform.position;
                shape.transform.rotation = Quaternion.Euler(0f, 0f, aimAngle);
            }

            // 몸도 겨눈 쪽을 본다. 조준이 도는데 그림이 가만히 있으면 어디를 노리는지 읽히지 않는다.
            PlayAction("Charge", aim);

            if (cc != null)
            {
                if (PlayerInSector(aim))
                {
                    exposure += Time.deltaTime;
                    cc.ApplySlow(Mathf.Lerp(1f, maxSlowFactor,
                        Mathf.Clamp01(exposure / maxSlowExposure)), 0.3f);
                }
                else
                {
                    exposure = Mathf.Max(0f, exposure - exposureDecay * Time.deltaTime);
                }
            }
            yield return null;
        }
    }

    private Vector2 CurrentAim
    {
        get
        {
            float radians = aimAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }

    // 분사 밖의 시간에도 노출은 서서히 녹는다.
    // Update로 두면 기반 EnemyAbility의 Update(시전 판단)를 가려 버리므로 LateUpdate를 쓴다.
    private void LateUpdate()
    {
        if (!IsCasting && exposure > 0f)
            exposure = Mathf.Max(0f, exposure - exposureDecay * Time.deltaTime);
    }

    private bool PlayerInSector(Vector2 aim)
    {
        if (Player == null) return false;
        Vector2 offset = PlayerPosition - (Vector2)transform.position;
        if (offset.magnitude > reach) return false;
        return Vector2.Angle(aim, offset) <= sweepAngle * 0.5f;
    }

    private static float AngleOf(Vector2 direction) =>
        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
}
