using System.Collections;
using UnityEngine;

/// <summary>
/// 나인테일의 공격. 입가에 불꽃이 차오르는 준비 동작과 함께 부채꼴 예고를 띄우고,
/// 넓은 부채꼴로 화염을 분사한다. 예고하는 동안에는 플레이어를 따라 조준을 돌리지만,
/// <b>뿜기 시작하는 순간 방향이 고정</b>된다 — 예고를 본 뒤 부채꼴 밖이나 등 뒤로
/// 돌아가는 것이 정답이다.
///
/// 화염에는 직접 피해만 있다. 감속도, 밀치기도, 바닥에 남는 불(장판)도 없다 —
/// 분사가 끝나면 그 자리는 즉시 안전하다. 대신 분사가 끝나면 과열로 한동안 정지한다.
/// 넓게 지지는 대신 쓰고 나면 빈틈이 큰, 전형적인 후열이다.
/// </summary>
public class EnemyFlameConeAbility : EnemyAbility
{
    [Header("분사")]
    [Tooltip("예고 시간. 부채꼴이 플레이어를 따라 돌다가 분사 순간 고정된다.")]
    [SerializeField, Min(0.1f)] private float windup = 0.75f;
    [Tooltip("부채꼴의 반지름(사거리).")]
    [SerializeField, Min(1f)] private float coneRange = 5f;
    [Tooltip("부채꼴의 전체 각도.")]
    [SerializeField, Range(10f, 180f)] private float coneAngle = 70f;
    [Tooltip("화염을 뿜는 시간.")]
    [SerializeField, Min(0.2f)] private float sprayDuration = 1.1f;
    [SerializeField, Min(0)] private int damage = 10;
    [Tooltip("부채꼴 안에 서 있으면 이 간격마다 맞는다.")]
    [SerializeField, Min(0.1f)] private float tickInterval = 0.45f;

    [Header("과열")]
    [Tooltip("분사가 끝난 뒤 과열로 정지하는 시간.")]
    [SerializeField, Min(0f)] private float overheatDuration = 2f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.3f);
    [SerializeField] private Color flameInner = new Color(1f, 0.85f, 0.3f, 0.85f);
    [SerializeField] private Color flameOuter = new Color(1f, 0.45f, 0.1f, 0.7f);

    private const float MuzzleOffset = 0.45f;

    protected override IEnumerator Perform()
    {
        // 예고 — 부채꼴이 플레이어를 따라 돈다. 입가의 불덩이가 커지며 "곧 뿜는다"를 알린다.
        Vector2 aim = DirectionToPlayer;
        AttackTelegraph telegraph = AttackTelegraph.CreateSector(
            EffectRoot, transform.position, coneRange, AngleOf(aim), coneAngle, warningColor);
        telegraph.Pulse(windup);
        PlayAction("Shoot", aim);

        float windupEnd = Time.time + windup;
        float chargeSize = 0.12f;
        float nextPuff = 0f;
        while (Time.time < windupEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            aim = DirectionToPlayer;
            if (telegraph != null)
            {
                telegraph.transform.rotation = Quaternion.Euler(0f, 0f, AngleOf(aim));
                telegraph.transform.position = transform.position;
            }
            PlayAction("Shoot", aim);

            // 입가에 차오르는 불꽃. 크기가 커질수록 임박했다는 뜻이다.
            chargeSize = Mathf.MoveTowards(chargeSize, 0.34f, Time.deltaTime * 0.35f);
            if (Time.time >= nextPuff)
            {
                nextPuff = Time.time + 0.06f;
                AttackTelegraph.CreateCircle(EffectRoot,
                    (Vector2)transform.position + aim * MuzzleOffset, chargeSize, flameInner).Hold(0.08f);
            }
            yield return null;
        }
        if (Health.IsDead) yield break;

        // 분사 — 이 순간 방향이 고정된다. 이후에는 조준을 돌리지 않는다.
        Vector2 locked = aim;
        float sprayEnd = Time.time + sprayDuration;
        float nextTick = 0f;
        float nextFlame = 0f;
        while (Time.time < sprayEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;

            // 불꽃 그림 — 부채꼴 안 아무 곳에나 잠깐 피었다 사라진다. 판정과 무관한 그림이라
            // 바닥에 아무것도 남기지 않는다. 프레임마다가 아니라 일정 간격으로만 피운다.
            if (Time.time >= nextFlame)
            {
                nextFlame = Time.time + 0.05f;
                for (int i = 0; i < 4; i++)
                {
                    float t = Random.value;
                    float dist = Mathf.Lerp(MuzzleOffset, coneRange, Mathf.Sqrt(t));
                    float spread = (Random.value - 0.5f) * coneAngle;
                    Vector2 dir = Rotate(locked, spread);
                    Color color = Color.Lerp(flameInner, flameOuter, t);
                    float size = Mathf.Lerp(0.16f, 0.34f, t);
                    AttackTelegraph.CreateCircle(EffectRoot,
                        (Vector2)transform.position + dir * dist, size, color).Hold(0.16f);
                }
            }

            // 피해 — 부채꼴 안에 있는 동안 일정 간격으로. 나가면 그 즉시 안전하다.
            if (Time.time >= nextTick && PlayerHealth != null &&
                !PlayerHealth.IsDead && !PlayerHealth.IsInvincible)
            {
                Vector2 offset = PlayerPosition - (Vector2)transform.position;
                if (offset.magnitude <= coneRange + 0.3f &&
                    Vector2.Angle(locked, offset) <= coneAngle * 0.5f)
                {
                    PlayerHealth.TakeDamage(damage);
                    nextTick = Time.time + tickInterval;
                }
            }
            yield return null;
        }
        if (Health.IsDead) yield break;

        // 과열 — 무방비로 정지. 넓게 지진 값이다.
        StopAction();
        float overheatEnd = Time.time + overheatDuration;
        while (Time.time < overheatEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }

    private static float AngleOf(Vector2 direction) =>
        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
