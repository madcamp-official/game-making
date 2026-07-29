using System.Collections;
using UnityEngine;

/// <summary>
/// 성원숭의 연속 돌진 공격. 주먹을 들어 올린 준비 자세를 보인 뒤, 플레이어 쪽으로
/// <b>짧게 돌진하며</b> 후려치기를 최대 <see cref="maxDashes"/>번 반복한다.
///
/// 돌진 한 번마다 <b>어디로 뛸지(경로)와 어디가 맞는지(피해 범위)를 미리 그린다.</b>
/// 예고 없이 몸이 날아오면 읽을 수가 없고, 경로만 그리면 스쳐 지나간 자리가 안전한지
/// 알 수 없다 — 둘을 같이 그려야 "옆으로 반 걸음"이 정답이라는 게 보인다.
///
/// 방향은 <b>돌진을 시작할 때만</b> 다시 잡고 도중에는 고정한다. 그래서 같은 방향으로
/// 도망치면 따라붙지만, 예고선 옆으로 비키면 빗나간다.
///
/// 한 번이라도 맞히면 거기서 멈추고 짧게 쉰다. 세 번을 다 빗나가면 지쳐서 길게 멈춘다 —
/// 전부 읽어낸 쪽에게 확실한 반격 창을 주는 것이 이 층의 규칙이다.
/// </summary>
public class EnemyComboMeleeAbility : EnemyAbility
{
    [Header("동작")]
    [Tooltip("돌진 타격 동작 상태 이름. 돌진마다 처음부터 다시 재생한다.")]
    [SerializeField] private string actionState = "MultiStrike";
    [Tooltip("주먹을 들어 올린 정지 자세. 비우면 준비 동작 없이 바로 돌진한다.")]
    [SerializeField] private string readyState = "Ready";

    [Header("돌진")]
    [Tooltip("한 번 시전에 최대 몇 번 돌진하는지. 맞히면 그 자리에서 끝난다.")]
    [SerializeField, Min(1)] private int maxDashes = 3;
    [Tooltip("첫 돌진 전 준비 자세로 서 있는 시간.")]
    [SerializeField, Min(0f)] private float windup = 0.45f;
    [Tooltip("돌진마다 경로와 피해 범위를 그려 두는 시간. 이 시간이 곧 피할 시간이다.")]
    [SerializeField, Min(0.05f)] private float telegraph = 0.4f;
    [Tooltip("한 번에 앞으로 나아가는 거리.")]
    [SerializeField, Min(0.5f)] private float dashDistance = 2.2f;
    [SerializeField, Min(1f)] private float dashSpeed = 9f;
    [Tooltip("돌진이 시작되고 실제로 맞기까지의 시간. AnimData HitFrame에 맞춘다.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.18f;
    [Tooltip("돌진 하나가 끝나고 다음 돌진까지의 간격. 플레이어 피격 무적(0.5초)보다 " +
             "길어야 연속 타격이 무적에 먹히지 않는다.")]
    [SerializeField, Min(0f)] private float betweenDashes = 0.3f;

    [Header("판정")]
    [Tooltip("몸 표면에서 더 뻗는 사거리.")]
    [SerializeField, Min(0.1f)] private float reach = 1.2f;
    [SerializeField, Range(20f, 360f)] private float sweepAngle = 150f;
    [Tooltip("돌진 한 번의 피해.")]
    [SerializeField, Min(0)] private int damage = 14;

    [Header("후딜")]
    [Tooltip("맞혔을 때 멈추는 시간. 맞히면 남은 돌진을 포기하고 여기로 온다.")]
    [SerializeField, Min(0f)] private float hitPause = 0.7f;
    [Tooltip("전부 빗나갔을 때 지쳐서 멈추는 시간.")]
    [SerializeField, Min(0f)] private float missPause = 1.6f;

    [Header("예고 색")]
    // 2층 바닥이 밝은 모래(연노랑)라, 주황 계열을 옅게 얹으면 배경에 묻혀 아예 안 보인다.
    // 경로는 바닥보다 어두운 보라빛으로, 피해 범위는 진한 붉은색으로 대비를 준다.
    [Tooltip("돌진 경로. 몸이 지나갈 자리다.")]
    [SerializeField] private Color pathColor = new Color(0.3f, 0.2f, 0.42f, 0.42f);
    [Tooltip("피해 범위. 실제로 맞는 자리다.")]
    [SerializeField] private Color hitColor = new Color(0.88f, 0.12f, 0.2f, 0.52f);

    /// <summary>예고 부채꼴을 몸 표면이 아니라 중심에서 그려야 해서 몸 반지름만큼 더한다.</summary>
    private float BodyRadius
    {
        get
        {
            Collider2D col = GetComponent<Collider2D>();
            return col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) : 0.5f;
        }
    }

    /// <summary>
    /// 예고를 그릴 중심. 판정은 <b>돌진이 끝난 자리가 아니라 타격 프레임의 자리</b>에서 난다 —
    /// hitDelay(0.18초) 동안 나아간 거리까지다. 돌진 끝에 그리면 그린 자리와 맞는 자리가
    /// 반 칸 넘게 어긋나, 예고를 믿고 비킨 쪽이 억울해진다.
    /// </summary>
    private float HitTravel => Mathf.Min(dashDistance, hitDelay * dashSpeed);

    /// <summary>
    /// 판정 반지름을 중심 거리로 환산한 값. <see cref="EnemyAbility.PlayerWithinSector"/>는
    /// 몸 <b>표면</b> 사이로 재므로, 그림으로 옮기려면 두 몸의 반지름을 모두 더해야 한다.
    /// 덜 그리면 예고 밖에 서 있다가 맞는다 — 넘칠지언정 모자라면 안 된다.
    /// </summary>
    private float TelegraphRadius
    {
        get
        {
            float playerHalf = 0.3f;
            if (PlayerHealth != null)
            {
                Collider2D col = PlayerHealth.GetComponent<Collider2D>();
                if (col != null) playerHalf = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
            }
            return reach + BodyRadius + playerHalf;
        }
    }

    protected override IEnumerator Perform()
    {
        // 준비 — 제자리에서 주먹을 들어 올린다. 시선은 이때까지 플레이어를 따라간다.
        float readyEnd = Time.time + windup;
        while (Time.time < readyEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            if (!string.IsNullOrEmpty(readyState)) PlayAction(readyState, DirectionToPlayer);
            yield return null;
        }
        if (Health.IsDead) yield break;

        bool connected = false;
        for (int i = 0; i < maxDashes && !connected && !Health.IsDead; i++)
        {
            // 이 돌진의 방향은 여기서 고정된다. 이후로는 플레이어를 다시 쫓지 않는다.
            Vector2 aim = DirectionToPlayer;
            yield return Telegraph(aim);
            if (Health.IsDead) yield break;

            yield return Dash(aim, hit => connected = hit);

            if (!connected && i + 1 < maxDashes)
            {
                float gap = Time.time + betweenDashes;
                while (Time.time < gap && !Health.IsDead)
                {
                    Body.linearVelocity = Vector2.zero;
                    yield return null;
                }
            }
        }
        if (Health.IsDead) yield break;

        // 후딜 — 맞혔으면 짧게, 세 번 다 빗나갔으면 지쳐서 길게.
        StopAction();
        float pauseEnd = Time.time + (connected ? hitPause : missPause);
        while (Time.time < pauseEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }

    /// <summary>돌진 경로와 피해 범위를 함께 그린다. 그리는 동안 제자리에 선다.</summary>
    private IEnumerator Telegraph(Vector2 aim)
    {
        Vector2 origin = transform.position;

        // 경로 — 몸이 지나갈 띠. 폭은 몸통 굵기에 맞춘다.
        AttackTelegraph path = AttackTelegraph.CreateLine(
            EffectRoot, origin, aim, dashDistance, BodyRadius * 2f, pathColor);
        // 피해 범위 — 타격 프레임에 몸이 있을 자리에서 앞쪽 부채꼴. 실제 판정과 같은 각도·거리다.
        AttackTelegraph zone = AttackTelegraph.CreateSector(
            EffectRoot, origin + aim * HitTravel, TelegraphRadius,
            Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, sweepAngle, hitColor);
        path.Pulse(telegraph);
        zone.Pulse(telegraph);

        float end = Time.time + telegraph;
        while (Time.time < end && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }

    /// <summary>정해진 방향으로 몸을 던지며 한 번 때린다.</summary>
    private IEnumerator Dash(Vector2 aim, System.Action<bool> report)
    {
        ReplayAction(actionState, aim);

        float duration = dashDistance / Mathf.Max(0.01f, dashSpeed);
        float elapsed = 0f;
        bool resolved = false;
        bool hit = false;

        // 돌진 시간과 타격 시점 중 늦은 쪽까지 돈다. 타격이 돌진보다 늦게 오는 설정에서도
        // 판정이 통째로 빠지지 않게 한다.
        float total = Mathf.Max(duration, hitDelay);
        while (elapsed < total && !Health.IsDead)
        {
            Body.linearVelocity = elapsed < duration ? aim * dashSpeed : Vector2.zero;

            if (!resolved && elapsed >= hitDelay)
            {
                resolved = true;
                if (PlayerWithinSector(aim, reach, sweepAngle) &&
                    PlayerHealth != null && !PlayerHealth.IsDead && !PlayerHealth.IsInvincible)
                {
                    PlayerHealth.TakeDamage(damage);
                    hit = true;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Body.linearVelocity = Vector2.zero;
        report(hit);
    }
}
