using System.Collections;
using UnityEngine;

/// <summary>
/// 성원숭의 연속 근접 공격. 붙어서 멈춘 뒤 주먹을 들어 올리는 준비 자세(Ready)를 잠깐
/// 보여 주고, 플레이어 쪽으로 전진하며 MultiStrike를 두 번 연달아 휘두른다.
///
/// 각 타는 <b>시작할 때만</b> 방향을 다시 잡고 동작 중에는 고정한다 — 같은 방향으로
/// 계속 도망치면 전진이 따라붙어 맞지만, 휘두르는 옆으로 비켜서면 빗나간다.
/// 몸이 닿아 있어도 타격 프레임이 아니면 피해가 없다.
///
/// 한 대라도 맞히면 짧게(<see cref="hitPause"/>), 전부 빗나가면 지쳐서 길게
/// (<see cref="missPause"/>) 멈춘다 — 다 피해낸 쪽에게 확실한 반격 창을 준다.
/// </summary>
public class EnemyComboMeleeAbility : EnemyAbility
{
    [Header("동작")]
    [Tooltip("연타 동작 상태 이름. 한 타마다 처음부터 다시 재생한다.")]
    [SerializeField] private string actionState = "MultiStrike";
    [Tooltip("주먹을 들어 올린 정지 자세. 비우면 준비 동작 없이 바로 때린다.")]
    [SerializeField] private string readyState = "Ready";

    [Header("연타")]
    [SerializeField, Min(1)] private int hits = 2;
    [Tooltip("준비 자세로 서 있는 시간. 이때가 '온다'를 읽는 시간이다.")]
    [SerializeField, Min(0f)] private float windup = 0.4f;
    [Tooltip("한 타의 전체 길이. 시트 재생(0.5초)보다 조금 길다 — 플레이어 피격 무적이 " +
             "0.5초라, 타 사이가 그보다 짧으면 두 번째 타가 무적에 흡수되어 절대 안 맞는다.")]
    [SerializeField, Min(0.1f)] private float swingDuration = 0.62f;
    [Tooltip("타가 시작되고 실제로 맞기까지의 시간. AnimData HitFrame에 맞춘다.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.18f;
    [Tooltip("타 도중 몸이 앞으로 나가는 구간(시작~끝, 초). 시트의 돌진 프레임에 맞춘다.")]
    [SerializeField] private Vector2 lungeWindow = new Vector2(0.07f, 0.23f);
    [SerializeField, Min(0f)] private float lungeSpeed = 6.5f;
    [Tooltip("몸 표면에서 더 뻗는 사거리.")]
    [SerializeField, Min(0.1f)] private float reach = 1.2f;
    [SerializeField, Range(20f, 360f)] private float sweepAngle = 150f;
    [Tooltip("한 타의 피해. 두 타 다 맞으면 두 배로 들어간다.")]
    [SerializeField, Min(0)] private int damage = 8;

    [Header("후딜")]
    [Tooltip("한 대라도 맞혔을 때 멈추는 시간.")]
    [SerializeField, Min(0f)] private float hitPause = 0.7f;
    [Tooltip("전부 빗나갔을 때 지쳐서 멈추는 시간.")]
    [SerializeField, Min(0f)] private float missPause = 1.5f;

    protected override IEnumerator Perform()
    {
        // 준비 — 제자리에 서서 주먹을 들어 올린다. 시선은 이때부터 플레이어를 따라간다.
        float readyEnd = Time.time + windup;
        while (Time.time < readyEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            if (!string.IsNullOrEmpty(readyState)) PlayAction(readyState, DirectionToPlayer);
            yield return null;
        }
        if (Health.IsDead) yield break;

        bool anyHit = false;
        for (int i = 0; i < hits; i++)
        {
            // 이 타의 방향은 여기서 고정된다. 이후에는 플레이어를 다시 쫓지 않는다.
            Vector2 aim = DirectionToPlayer;
            ReplayAction(actionState, aim);

            bool resolved = false;
            float elapsed = 0f;
            while (elapsed < swingDuration && !Health.IsDead)
            {
                bool lunging = elapsed >= lungeWindow.x && elapsed < lungeWindow.y;
                Body.linearVelocity = lunging ? aim * lungeSpeed : Vector2.zero;

                if (!resolved && elapsed >= hitDelay)
                {
                    resolved = true;
                    if (PlayerWithinSector(aim, reach, sweepAngle) &&
                        PlayerHealth != null && !PlayerHealth.IsDead && !PlayerHealth.IsInvincible)
                    {
                        PlayerHealth.TakeDamage(damage);
                        anyHit = true;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            if (Health.IsDead) yield break;
        }

        // 후딜 — 맞혔으면 짧게, 다 빗나갔으면 지쳐서 길게.
        StopAction();
        float pauseEnd = Time.time + (anyHit ? hitPause : missPause);
        while (Time.time < pauseEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }
}
