using System.Collections;
using UnityEngine;

/// <summary>
/// 성원숭의 연속 돌진 공격. 주먹을 들어 올린 준비 자세를 보인 뒤, 플레이어 쪽으로
/// <b>짧게 돌진하며</b> 후려치기를 최대 <see cref="maxDashes"/>번 반복한다.
///
/// 돌진 한 번마다 <b>앞으로 뻗는 직사각형 하나</b>를 미리 그린다. 그 사각형이 곧
/// 그림이자 판정이라, 예고선 밖으로 반 걸음이 언제나 정답이다.
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

    // ---------------------------------------------------------------- 판정
    //
    // 처음에는 반지름 1.2 · 150도짜리 부채꼴이었다. 몸 반지름과 플레이어 반지름을 더하면
    // 실제로는 반지름 1.9의 <b>반원에 가까운</b> 범위라, 옆으로 비켜도 여전히 안에 들어 있는
    // 일이 잦았다. 부채꼴은 "어디까지 도는가"를 눈대중해야 하는데 그 각도가 넓을수록
    // 눈대중이 통하지 않는다.
    //
    // 그다음에는 직사각형이 되었지만 <b>둘</b>이었다 — 몸이 지나갈 좁고 긴 복도와, 그 끝에
    // 붙은 정사각형에 가까운 판정 상자. 이어 붙인 자리에서 폭이 갑자기 벌어져 여전히
    // 눈으로 재기 어려웠고, 무엇보다 판정이 몸에서 1.6칸 앞에서야 시작해 <b>코앞이 안전</b>했다.
    //
    // 지금은 <b>몸 앞에서 시작하는 직사각형 하나</b>다. 규칙이 "앞이면 맞고 옆이면 안 맞는다"
    // 하나로 줄어, 예고선을 보고 옆으로 반 걸음이 그대로 정답이 된다.
    //
    // 길이는 늘리되 <b>닿는 끝은 짧아졌다.</b> 그리는 사각형이 2.8칸(1.5 → 2.5 + 플레이어
    // 반지름)으로 복도(2.2)보다 길어졌지만, 예전에는 상자가 1.6칸 앞에서 시작해 1.8칸을
    // 더 뻗어 <b>출발점에서 3.4칸</b>까지 닿았다. 앞은 짧아지고 발밑이 채워진 셈이다.
    [Header("판정")]
    [Tooltip("출발한 자리에서 앞으로 뻗는 길이. 판정도 예고도 여기서 시작한다.")]
    [SerializeField, Min(0.1f)] private float hitLength = 2.5f;
    [Tooltip("직사각형의 전체 폭. 이 값이 곧 '옆으로 얼마나 비켜야 사는가'다.")]
    [SerializeField, Min(0.1f)] private float hitWidth = 1.2f;
    [Tooltip("돌진 한 번의 피해.")]
    [SerializeField, Min(0)] private int damage = 14;

    [Header("후딜")]
    [Tooltip("맞혔을 때 멈추는 시간. 맞히면 남은 돌진을 포기하고 여기로 온다.")]
    [SerializeField, Min(0f)] private float hitPause = 0.7f;
    [Tooltip("전부 빗나갔을 때 지쳐서 멈추는 시간.")]
    [SerializeField, Min(0f)] private float missPause = 1.6f;

    [Header("예고 색")]
    // 2층 바닥이 밝은 모래(연노랑)라, 주황 계열을 옅게 얹으면 배경에 묻혀 아예 안 보인다.
    //
    // 색이 하나인 것도 뜻이 있다. 도형이 둘이던 시절에는 진하기를 달리해 "지나갈 자리"와
    // "맞는 자리"를 갈랐는데, 이제 그 둘이 같은 사각형이라 나눌 것이 없다.
    // 색은 "여기 서 있으면 맞는다" 하나만 뜻한다.
    [Tooltip("피해 범위. 그린 그대로가 맞는 자리다.")]
    [SerializeField] private Color hitColor = new Color(0.88f, 0.12f, 0.2f, 0.52f);

    /// <summary>플레이어 몸의 반지름. 판정을 중심점 기준으로 옮길 때 쓴다.</summary>
    private float PlayerRadius
    {
        get
        {
            if (PlayerHealth == null) return 0.3f;
            Collider2D col = PlayerHealth.GetComponent<Collider2D>();
            return col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) : 0.3f;
        }
    }

    /// <summary>
    /// 그리고 또 재는 직사각형. <b>플레이어의 중심점</b>이 이 안에 있으면 맞는다.
    ///
    /// 몸끼리 스치는 것까지 세려면 두 몸의 반지름을 더해야 하는데, 그러면 그린 사각형보다
    /// 맞는 자리가 넓어져 예고 밖에 서 있다가 맞는다. 대신 <b>사각형 쪽을 플레이어 반지름만큼
    /// 부풀려 두고</b> 중심점으로 판정한다 — 그리는 것과 재는 것이 완전히 같은 도형이 된다.
    /// 넘칠지언정 모자라면 안 된다는 원칙은 그대로다.
    /// </summary>
    /// <param name="origin">타격 프레임의 몸 중심.</param>
    private void HitBox(Vector2 origin, Vector2 aim, out Vector2 center, out float length, out float width)
    {
        float playerHalf = PlayerRadius;
        length = hitLength + playerHalf;
        width = hitWidth + playerHalf * 2f;
        center = origin + aim * (length * 0.5f);
    }

    private bool PlayerInHitBox(Vector2 origin, Vector2 aim)
    {
        if (Player == null) return false;
        HitBox(origin, aim, out Vector2 center, out float length, out float width);
        Vector2 offset = PlayerPosition - center;
        Vector2 side = new Vector2(-aim.y, aim.x);
        return Mathf.Abs(Vector2.Dot(offset, aim)) <= length * 0.5f &&
               Mathf.Abs(Vector2.Dot(offset, side)) <= width * 0.5f;
    }

    protected override IEnumerator Perform()
    {
        // 준비 — 제자리에서 주먹을 들어 올린다. 시선은 이때까지 플레이어를 따라간다.
        float readyEnd = Time.time + windup;
        while (Time.time < readyEnd && !Health.IsDead)
        {
            HoldPosition();
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
                    HoldPosition();
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
            HoldPosition();
            yield return null;
        }
    }

    /// <summary>
    /// 앞으로 뻗는 <b>직사각형 하나</b>를 그린다. 그리는 동안 제자리에 선다.
    ///
    /// 예전에는 도형이 둘이었다 — 몸이 지나갈 좁고 긴 복도와, 그 끝 머리 자리에 붙은
    /// 정사각형에 가까운 판정 상자. 한 번의 공격인데도 <b>두 개가 붙어 있는 모양</b>으로
    /// 읽혔고, 이어 붙인 자리에서 폭이 갑자기 벌어져 어디까지가 위험한지 눈으로 재기 어려웠다.
    ///
    /// 이제 하나다. 그 하나가 곧 <b>그림이자 판정</b>이고(<see cref="PlayerInHitBox"/>가 같은
    /// <see cref="HitBox"/>를 쓴다), 몸 앞에서 시작하므로 <b>코앞이 안전지대가 아니다</b> —
    /// 예전에는 판정 상자가 몸에서 1.6칸 앞에서 시작해, 바짝 붙어 있으면 돌진이 몸을
    /// 관통하고도 맞지 않는 일이 있었다.
    /// </summary>
    private IEnumerator Telegraph(Vector2 aim)
    {
        HitBox(transform.position, aim, out _, out float boxLength, out float boxWidth);
        AttackTelegraph zone = AttackTelegraph.CreateLine(
            EffectRoot, transform.position, aim, boxLength, boxWidth, hitColor);
        zone.Pulse(telegraph);

        float end = Time.time + telegraph;
        while (Time.time < end && !Health.IsDead)
        {
            HoldPosition();
            // 그린 자리를 몸에 붙여 둔다. 예고하는 동안 넉백으로 밀려날 수 있는데
            // (HoldPosition이 그 0.15초는 놓아 준다), 그림만 처음 자리에 남으면
            // 예고와 실제 판정이 어긋난다. CreateLine은 밑변이 아니라 <b>가운데</b>에
            // 오브젝트를 놓으므로 반 칸 앞으로 밀어 준다.
            if (zone != null)
                zone.transform.position = (Vector2)transform.position + aim * (boxLength * 0.5f);
            yield return null;
        }
    }

    /// <summary>정해진 방향으로 몸을 던지며 한 번 때린다.</summary>
    private IEnumerator Dash(Vector2 aim, System.Action<bool> report)
    {
        ReplayAction(actionState, aim);

        // 판정의 기준은 <b>출발한 자리</b>다. 예고를 마지막으로 그린 자리가 여기이므로,
        // 그린 사각형과 재는 사각형이 완전히 같아진다.
        //
        // 몸을 따라가게 두면 안 된다 — 타격 시점(hitDelay)에는 몸이 이미 앞으로 나아가 있어서,
        // 사각형째 딸려 가면 예고보다 한참 앞을 때리게 된다.
        Vector2 origin = transform.position;

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
                if (PlayerInHitBox(origin, aim) &&
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
