using System.Collections;
using UnityEngine;

/// <summary>
/// 쥬래곤의 냉기 분사 — 감속 지원 적. 넓은 부채꼴을 예고한 뒤 그 방향으로 일정 시간
/// 냉기를 뿜는다(Charge). 분사를 시작하면 방향을 바꾸지 않는다 — 처음 정한 방향을
/// 유지해야 빠르게 벗어나는 선택이 성립한다.
///
/// 직접 피해는 없다. 냉기에 노출된 시간이 쌓일수록 감속이 강해지고, 오래 버티면
/// 짧게 빙결된다. 빙결 직후에는 재빙결 면역이 있어(<see cref="PlayerCrowdControl"/>)
/// 연속으로 얼지 않는다. 노출을 끊으면 쌓인 시간이 서서히 줄어, 스치기만 한
/// 플레이어는 감속만 받고 끝난다.
/// </summary>
public class EnemyFrostBreathAbility : EnemyAbility
{
    [Header("냉기 부채꼴")]
    [SerializeField, Min(0.1f)] private float reach = 5.5f;
    [SerializeField, Range(10f, 180f)] private float sweepAngle = 70f;
    [SerializeField, Min(0.05f)] private float telegraph = 0.6f;
    [Tooltip("냉기를 뿜는 시간. 이 동안 방향은 고정이다.")]
    [SerializeField, Min(0.1f)] private float breathDuration = 1.8f;
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.45f);
    [SerializeField] private Color frostColor = new Color(0.6f, 0.85f, 1f, 0.4f);

    [Header("감속과 빙결")]
    [Tooltip("노출이 이만큼 쌓이면 감속이 최대가 된다(초).")]
    [SerializeField, Min(0.1f)] private float maxSlowExposure = 1.1f;
    [Tooltip("최대로 쌓였을 때 남는 이동 속도 비율.")]
    [SerializeField, Range(0.05f, 1f)] private float maxSlowFactor = 0.45f;
    [Tooltip("노출이 이만큼 쌓이면 빙결한다(초).")]
    [SerializeField, Min(0.1f)] private float freezeExposure = 1.5f;
    [SerializeField, Min(0f)] private float freezeDuration = 0.5f;
    [Tooltip("빙결이 풀린 뒤 다시 얼지 않는 시간.")]
    [SerializeField, Min(0f)] private float refreezeImmunity = 2.5f;
    [Tooltip("냉기 밖에서 쌓인 노출이 초당 줄어드는 양.")]
    [SerializeField, Min(0f)] private float exposureDecay = 1.6f;

    [Header("후딜")]
    [SerializeField, Min(0f)] private float recovery = 1.1f;

    /// <summary>쌓인 노출 시간. 시전이 끝나도 이어져, 연속 시전이 이어 붙는다.</summary>
    private float exposure;

    protected override IEnumerator Perform()
    {
        Vector2 aim = DirectionToPlayer;
        float centerAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

        // 1. 예고.
        AttackTelegraph warning = AttackTelegraph.CreateSector(
            EffectRoot, transform.position, reach, centerAngle, sweepAngle, warningColor);
        warning.Pulse(telegraph);
        yield return new WaitForSeconds(telegraph);
        if (Health.IsDead) yield break;

        // 2. 분사. 실제 판정 범위를 냉기색으로 계속 보여 준다.
        PlayAction("Charge", aim);
        AttackTelegraph frost = AttackTelegraph.CreateSector(
            EffectRoot, transform.position, reach, centerAngle, sweepAngle, frostColor);
        frost.Hold(breathDuration);

        PlayerCrowdControl cc = PlayerCrowdControl.Of(PlayerHealth);
        float breathEnd = Time.time + breathDuration;
        while (Time.time < breathEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            bool inside = PlayerInSector(aim);
            if (inside) exposure += Time.deltaTime;
            else exposure = Mathf.Max(0f, exposure - exposureDecay * Time.deltaTime);

            if (inside && cc != null)
            {
                float t = Mathf.Clamp01(exposure / maxSlowExposure);
                cc.ApplySlow(Mathf.Lerp(1f, maxSlowFactor, t), 0.3f);
                if (exposure >= freezeExposure && cc.CanFreeze)
                {
                    cc.Freeze(freezeDuration, refreezeImmunity);
                    exposure = 0f; // 얼렸으면 처음부터 다시 쌓아야 한다.
                }
            }
            yield return null;
        }
        if (Health.IsDead) yield break;

        yield return new WaitForSeconds(recovery);
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
}
