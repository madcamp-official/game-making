using UnityEngine;

/// <summary>
/// 이벤트 방의 NPC가 취하는 동작. 서 있는 동안 지정한 상태(수련 동작 등)를 반복하다가,
/// 이벤트 결과에 따라 다른 상태로 바꿔 준다 — 제자로 선택된 스승은 수련을 멈추고 쉰다.
///
/// 적이 아니라서 <see cref="EnemyAnimator"/>(Rigidbody·추적 방향 기반)를 쓰지 않는다.
/// 방향은 상태 이름에 굳어 있다: 행 0이 남쪽(아래)이다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EventNpcPose : MonoBehaviour
{
    [Tooltip("서 있는 동안 반복할 상태 이름. 예: Kick_0 — 남쪽(아래)을 보고 발차기.")]
    [SerializeField] private string initialState = "Idle_0";
    [Tooltip("수련 동작의 재생 배속. PMD 공격 동작은 0.5초 안에 끝나 그대로 틀면 난사처럼 보인다. " +
             "0.45면 발차기 한 번이 약 1초 — 잡몹들의 공격 속도쯤이 된다.")]
    [SerializeField, Range(0.1f, 1f)] private float trainingSpeed = 0.45f;

    private Animator animator;

    private void Awake() => animator = GetComponent<Animator>();

    private void Start()
    {
        animator.speed = trainingSpeed;
        Play(initialState);
    }

    /// <summary>상태를 바꾼다. 컨트롤러에 없는 이름이면 조용히 무시한다.</summary>
    public void Play(string state)
    {
        if (animator != null && animator.HasState(0, Animator.StringToHash(state)))
            animator.Play(state);
    }

    /// <summary>수련을 멈추고 쉰다 (남쪽을 본 채). 쉬는 숨은 제 속도로 쉰다.</summary>
    public void SetIdle()
    {
        if (animator != null) animator.speed = 1f;
        Play("Idle_0");
    }
}
