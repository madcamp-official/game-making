using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 콘팡의 공격. 버터플의 독가루와 같은 장판을 딱 하나만 깐다.
///
/// 장판 크기·피해·틱 간격은 보스 쪽과 같은 값을 기본으로 둔다
/// (<see cref="ButterfreeBossController"/>의 1페이즈 설정). 개수만 1개로 줄인 것이
/// 잡몹판과 보스판의 차이다.
///
/// 여러 마리가 같은 자리를 노리지 않도록, 노린 위치를 서로 공유하는 예약 목록에 올린다.
/// 콘팡들은 서로를 모르는 채 각자 플레이어를 조준하기 때문에, 이게 없으면
/// 두세 마리가 정확히 같은 지점에 장판을 겹쳐 깔아 한 개짜리 공격이 되어 버린다.
/// </summary>
public class EnemyPoisonAbility : EnemyAbility
{
    [Header("장판")]
    [Tooltip("버터플 1페이즈와 같은 크기.")]
    [SerializeField, Min(0f)] private float radius = 1.26f;
    [Tooltip("예고 원이 뜬 뒤 장판으로 바뀌기까지의 시간.")]
    [SerializeField, Min(0f)] private float windup = 0.99f;
    [SerializeField, Min(0f)] private float duration = 5.5f;
    [SerializeField, Min(0)] private int damage = 10;
    [SerializeField, Min(0f)] private float tickInterval = 1f;
    [Tooltip("플레이어가 이동하는 앞을 얼마나 내다볼지. 0이면 발밑에 깐다.")]
    [SerializeField, Min(0f)] private float predictLead = 0.6f;

    [Header("겹침 방지")]
    [Tooltip("다른 장판 중심과 최소한 반지름의 몇 배만큼 떨어뜨릴지. " +
             "반지름을 바꾸면 간격도 따라 움직이도록 배수로 둔다.")]
    [SerializeField, Min(0f)] private float separationScale = 1.5f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.75f, 0.35f, 0.85f, 0.35f);
    [SerializeField] private Color zoneColor = new Color(0.45f, 0.12f, 0.6f, 0.65f);

    /// <summary>비켜 볼 방향. 막혔을 때 이 순서로 한 칸씩 밀어 본다.</summary>
    private static readonly float[] DodgeAngles = { 0f, 90f, 180f, 270f, 45f, 135f, 225f, 315f };

    // 예약된 장판 자리. 콘팡들이 공유하므로 정적이다.
    private static readonly List<Vector2> claimCenters = new List<Vector2>();
    private static readonly List<float> claimExpiry = new List<float>();

    /// <summary>
    /// 플레이 모드를 다시 시작해도 정적 목록은 살아남는다. Time.time은 0으로 돌아가므로
    /// 지난 판의 예약이 "아직 유효한" 것으로 보여 모든 시전을 막아 버린다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClaims()
    {
        claimCenters.Clear();
        claimExpiry.Clear();
    }

    private Rigidbody2D playerBody;

    private float MinSeparation => radius * separationScale;

    protected override void Start()
    {
        base.Start();
        if (Player != null) playerBody = Player.GetComponent<Rigidbody2D>();
    }

    protected override IEnumerator Perform()
    {
        // 발밑에 그대로 깔면 예고를 보고 한 걸음만 옮겨도 100% 피해진다.
        // 이동 방향을 조금 내다봐서, 멈추거나 방향을 꺾게 만든다.
        Vector2 desired = PlayerPosition;
        if (playerBody != null) desired += playerBody.linearVelocity * predictLead;

        PruneClaims();
        if (!TryClaim(desired, out Vector2 target)) yield break;   // 낼 자리가 없으면 이번엔 거른다

        AttackTelegraph telegraph = AttackTelegraph.CreateCircle(EffectRoot, target, radius, warningColor);
        telegraph.Pulse(windup);
        PlayAction("Charge", target - (Vector2)transform.position);

        yield return new WaitForSeconds(windup);
        StopAction();
        if (Health.IsDead) yield break;

        DamageZone.Spawn(EffectRoot, target, radius, duration, damage, tickInterval, zoneColor);
    }

    /// <summary>
    /// <paramref name="desired"/>를 쓰거나, 막혀 있으면 한 칸 옆으로 밀어 빈 자리를 찾는다.
    /// 찾으면 예약까지 걸고 true. 여덟 방향이 모두 막혔으면 false.
    /// </summary>
    private bool TryClaim(Vector2 desired, out Vector2 target)
    {
        float separation = MinSeparation;
        for (int i = 0; i < DodgeAngles.Length; i++)
        {
            Vector2 candidate = i == 0
                ? desired
                : desired + Rotate(Vector2.right, DodgeAngles[i]) * separation;

            if (!IsClaimed(candidate, separation))
            {
                claimCenters.Add(candidate);
                // 장판이 사라질 때까지 자리를 잡아 둔다.
                claimExpiry.Add(Time.time + windup + duration);
                target = candidate;
                return true;
            }
        }
        target = desired;
        return false;
    }

    private static bool IsClaimed(Vector2 point, float separation)
    {
        for (int i = 0; i < claimCenters.Count; i++)
            if (Vector2.Distance(claimCenters[i], point) < separation) return true;
        return false;
    }

    private static void PruneClaims()
    {
        for (int i = claimCenters.Count - 1; i >= 0; i--)
        {
            if (Time.time < claimExpiry[i]) continue;
            claimCenters.RemoveAt(i);
            claimExpiry.RemoveAt(i);
        }
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
