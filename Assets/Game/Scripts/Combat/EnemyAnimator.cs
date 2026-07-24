using UnityEngine;

/// <summary>
/// 적의 이동 속도에 따라 Idle/Walk 8방향 애니메이션을 재생한다.
/// 상태 이름 규칙은 PlayerAnimator와 동일 (Idle_행 / Walk_행).
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAnimator : MonoBehaviour
{
    // PMDCollab 시트 행 순서: 0=남, 1=남동, 2=동, 3=북동, 4=북, 5=북서, 6=서, 7=남서
    private static readonly int[] RowForOctant = { 2, 3, 4, 5, 6, 7, 0, 1 };

    private Animator animator;
    private Rigidbody2D body;
    private Vector2 facing = Vector2.down;
    private string currentState = "";

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        Vector2 velocity = body.linearVelocity;
        bool moving = velocity.sqrMagnitude > 0.01f;
        if (moving) facing = velocity.normalized;

        int octant = Mathf.RoundToInt(Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg / 45f);
        int row = RowForOctant[(octant + 8) % 8];

        string state = (moving ? "Walk_" : "Idle_") + row;
        if (state != currentState)
        {
            currentState = state;
            animator.Play(state);
        }
    }
}
