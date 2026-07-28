using UnityEngine;

/// <summary>
/// 적의 이동 속도에 따라 Idle/Walk 8방향 애니메이션을 재생한다.
/// 상태 이름 규칙은 PlayerAnimator와 동일 (Idle_행 / Walk_행).
///
/// 특수 공격을 시전하는 동안에는 <see cref="SetActionState"/>로 다른 동작(Charge 등)을
/// 덮어쓸 수 있다. 이때 바라보는 방향은 마지막으로 움직인 방향이 아니라
/// 시전 시점에 정해 준 방향을 쓴다 — 예고선과 몸이 다른 곳을 보면 어디로 날아올지 읽을 수 없다.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAnimator : MonoBehaviour
{
    // PMDCollab 시트 행 순서: 0=남, 1=남동, 2=동, 3=북동, 4=북, 5=북서, 6=서, 7=남서
    private static readonly int[] RowForOctant = { 2, 3, 4, 5, 6, 7, 0, 1 };

    private Animator animator;
    private Rigidbody2D body;
    private EnemyController controller;
    private Vector2 facing = Vector2.down;
    private string currentState = "";
    private string actionState;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
        controller = GetComponent<EnemyController>();
    }

    /// <summary>
    /// 시전 동작으로 고정한다. <paramref name="stateName"/>이 비면 Idle/Walk로 돌아간다.
    /// 해당 상태가 컨트롤러에 없으면 조용히 무시된다 (Charge 시트가 없는 적도 있다).
    ///
    /// 자세를 오래 유지해야 하면 클립을 끝에 멈추는 대신 한 장짜리 정지 상태를 따로 만들어 쓴다
    /// (고지의 Guard). 끝 프레임이 원하는 자세라는 보장이 없기 때문이다.
    /// </summary>
    public void SetActionState(string stateName, Vector2 lookDirection)
    {
        if (lookDirection.sqrMagnitude > 0.0001f) facing = lookDirection.normalized;
        actionState = string.IsNullOrEmpty(stateName) ? null : stateName;
    }

    public void ClearActionState() => actionState = null;

    private void Update()
    {
        Vector2 velocity = body.linearVelocity;
        bool moving = velocity.sqrMagnitude > 0.01f;
        // 몸이 닿아 있으면 속도가 0이지만 계속 밀고 있는 상태다. 이때 멈춘 그림으로 바뀌면
        // 붙어서 때리는 내내 굳어 보이므로, 추적 중이면 걷기를 유지한다.
        bool engaged = controller != null && controller.IsEngaged;

        // 시전 중에는 방향을 고정한다. 돌진처럼 속도가 실리는 동작에서 몸이 홱 돌아버리면
        // 예고를 보고 잡은 위치가 무의미해진다.
        if (actionState == null)
        {
            if (moving) facing = velocity.normalized;
            else if (engaged) facing = controller.FacingDirection;
        }

        int octant = Mathf.RoundToInt(Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg / 45f);
        int row = RowForOctant[(octant + 8) % 8];

        string prefix = actionState ?? (moving || engaged ? "Walk" : "Idle");
        string state = prefix + "_" + row;
        if (state != currentState)
        {
            currentState = state;
            if (animator.HasState(0, Animator.StringToHash(state))) animator.Play(state);
        }
    }
}
