using System.Collections;
using UnityEngine;

/// <summary>
/// 나인테일의 공격. 입가에 불꽃이 차오르는 준비 동작과 함께 부채꼴 예고를 띄우고,
/// 부채꼴로 화염을 분사한다. 예고하는 동안에는 플레이어를 따라 조준을 돌리지만,
/// <b>뿜기 시작하는 순간 방향이 고정</b>된다 — 예고를 본 뒤 부채꼴 밖이나 등 뒤로
/// 돌아가는 것이 정답이다.
///
/// 조준이 도는 속도에는 상한이 있다 (<see cref="turnSpeed"/>). 예전에는 매 프레임 곧바로
/// 플레이어를 다시 겨눠서, <b>옆으로 도는 회피가 통하지 않았다</b> — 아무리 돌아도 부채꼴이
/// 붙어 다녀서 멀어지는 것 말고는 답이 없었다. 회전을 늦추면 "옆으로 돌아 각을 벌린다"가
/// 성립한다. 각도를 좁힌 것도 같은 이유다.
///
/// 그래도 옆으로 도는 쪽이 멀어지는 쪽보다 한참 빡빡해서 각도를 55°→45°, 사거리를 6.2→5.2로
/// 낮췄다. 대기 거리 4.2에서 옆으로 도는 플레이어의 각속도는 초당 68도, 조준은 25도이므로
/// 초당 43도씩 벌어진다 — 반각 22.5°를 벗어나는 데 0.52초, 예고 0.75초 안에 든다.
/// (55°였을 때는 0.64초라 예고가 거의 끝나서야 겨우 빠져나왔다.)
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
    [SerializeField, Min(1f)] private float coneRange = 5.2f;
    [Tooltip("부채꼴의 전체 각도.")]
    [SerializeField, Range(10f, 180f)] private float coneAngle = 45f;
    [Tooltip("예고 중 조준이 도는 최대 속도(초당 각도). 플레이어가 옆으로 돌 때의 각속도보다 " +
             "느려야 회피가 성립한다 — 거리 4.2에서 플레이어(속도 5)의 각속도는 초당 약 68도다. " +
             "0이면 곧바로 따라붙어 옆으로 도는 회피가 통하지 않는다.")]
    [SerializeField, Min(0f)] private float turnSpeed = 25f;
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
        float aimAngle = AngleOf(aim);
        while (Time.time < windupEnd && !Health.IsDead)
        {
            HoldPosition();

            // 곧바로 겨누지 않고 정해진 속도만큼만 돌린다. 이 한 줄이 "옆으로 돌아 피한다"를
            // 만든다 — 곧바로 따라붙으면 부채꼴이 몸에 붙어 다녀 멀어지는 것 말고는 답이 없다.
            aimAngle = turnSpeed > 0f
                ? Mathf.MoveTowardsAngle(aimAngle, AngleOf(DirectionToPlayer), turnSpeed * Time.deltaTime)
                : AngleOf(DirectionToPlayer);
            float radians = aimAngle * Mathf.Deg2Rad;
            aim = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

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
            HoldPosition();

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
            HoldPosition();
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
