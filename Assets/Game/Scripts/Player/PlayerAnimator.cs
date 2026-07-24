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

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
    }

    /// <summary>공격 애니메이션을 현재 방향으로 1회 재생한다.</summary>
    public void PlayAttack(float duration)
    {
        attackEndTime = Time.time + duration;
        currentState = "Attack_" + CurrentRow();
        animator.Play(currentState, 0, 0f);
    }

    private int CurrentRow()
    {
        Vector2 dir = controller.FacingDirection;
        int octant = Mathf.RoundToInt(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg / 45f);
        return RowForOctant[(octant + 8) % 8];
    }

    private void Update()
    {
        if (Time.time < attackEndTime) return; // 공격 모션 재생 중

        string state = (controller.IsMoving ? "Walk_" : "Idle_") + CurrentRow();
        if (state != currentState)
        {
            currentState = state;
            animator.Play(state);
        }
    }
}
