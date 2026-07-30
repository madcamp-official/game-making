using UnityEngine;

/// <summary>
/// PlayerController의 이동 상태와 바라보는 방향에 맞는 애니메이션 상태를 재생한다.
/// Animator에는 "Idle_0"~"Idle_7", "Walk_0"~"Walk_7" 상태가 있어야 하며
/// 숫자는 스프라이트 시트의 방향 행(row) 인덱스다.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    // 화면 8방향(octant: 0=동, 1=북동, 2=북, 3=북서, 4=서, 5=남서, 6=남, 7=남동)을
    // 스프라이트 시트의 행 인덱스로 변환하는 표.
    // PMDCollab 시트 행 순서: 0=남, 1=남동, 2=동, 3=북동, 4=북, 5=북서, 6=서, 7=남서
    private static readonly int[] RowForOctant = { 2, 3, 4, 5, 6, 7, 0, 1 };

    private Animator animator;
    private PlayerController controller;
    private string currentState = "";
    private float attackEndTime = -1f;
    /// <summary>비어 있지 않으면 동작이 끝날 때까지 이 동작을 유지하되, 방향(행)은 계속 따라간다.</summary>
    private string channelAction;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
    }

    /// <summary>공격 애니메이션을 현재 방향으로 1회 재생한다.</summary>
    public void PlayAttack(float duration) => PlayAction("Attack", duration);

    /// <summary>
    /// 지정한 동작("Shoot"·"Strike" 등)을 현재 방향으로 1회 재생한다. 캐릭터마다 공격
    /// 동작 시트가 달라서(이상해씨 Attack, 리자몽 Strike, 거북왕 Ricochet …) 이름을 받는다.
    /// 컨트롤러에 없는 동작이면 걷기·대기를 유지한다 — 개발 도구로 단계와 기술을 어긋나게
    /// 맞췄을 때(1단계 꼬부기에게 로켓박치기) 경고만 쌓이는 것을 막는다.
    /// </summary>
    public void PlayAction(string action, float duration)
    {
        channelAction = null;
        string state = action + "_" + CurrentRow();
        if (!animator.HasState(0, Animator.StringToHash(state))) return;
        attackEndTime = Time.time + duration;
        currentState = state;
        animator.Play(state, 0, 0f);
    }

    /// <summary>
    /// 이어지는 기술(화염방사·하이드로펌프)용. 동작을 유지한 채 <b>조준이 도는 대로 행을
    /// 갈아 끼운다</b> — 1회 재생과 달리 방향이 시전 도중 바뀌기 때문이다.
    /// </summary>
    public void BeginChannel(string action, float duration)
    {
        string state = action + "_" + CurrentRow();
        if (!animator.HasState(0, Animator.StringToHash(state))) return;
        channelAction = action;
        attackEndTime = Time.time + duration;
        currentState = state;
        animator.Play(state, 0, 0f);
    }

    /// <summary>채널이 중간에 끊겼을 때 즉시 걷기·대기로 되돌린다.</summary>
    public void EndChannel()
    {
        if (channelAction == null) return;
        channelAction = null;
        attackEndTime = -1f;
    }

    private int CurrentRow()
    {
        Vector2 dir = controller.FacingDirection;
        int octant = Mathf.RoundToInt(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg / 45f);
        return RowForOctant[(octant + 8) % 8];
    }

    private void Update()
    {
        if (Time.time < attackEndTime)
        {
            // 채널 중에는 조준 방향이 돌 때만 같은 동작의 다른 행으로 갈아 끼운다.
            if (channelAction != null)
            {
                string channelState = channelAction + "_" + CurrentRow();
                if (channelState != currentState)
                {
                    currentState = channelState;
                    animator.Play(channelState);
                }
            }
            return; // 공격·채널 모션 재생 중
        }
        channelAction = null;

        string state = (controller.IsMoving ? "Walk_" : "Idle_") + CurrentRow();
        if (state != currentState)
        {
            currentState = state;
            animator.Play(state);
        }
    }
}
