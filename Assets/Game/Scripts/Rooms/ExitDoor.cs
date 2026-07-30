using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 방 출구. 벽의 빈 구간을 메우는 단단한 충돌체이며, 열린 상태에서 플레이어가
/// 닿으면 다음 방으로 넘어간다.
///
/// 예전에는 열릴 때 충돌체를 트리거로 바꿨는데, 그러면 벽에 실제로 구멍이 뚫린다.
/// 트리거 진입 이벤트를 한 번이라도 놓치면(열리는 순간 이미 겹쳐 있었거나,
/// 다음 방으로 넘어가지 않는 게임 클리어 시점 등) 플레이어가 그대로 방 밖으로
/// 걸어 나갈 수 있었다. 지금은 항상 단단하게 두고 충돌로 판정한다.
///
/// 열리면 판정이 통로 <b>안쪽으로 물러난다</b>(<see cref="openDepth"/>). 문은 벽 구멍
/// 자리(x ±7.25)에 박혀 있어서, 그대로 두면 방 안에서 통로 입구를 스치기만 해도 다음 방으로
/// 넘어갔다 — 벽을 따라 오르내리다 발이 닿는 것만으로도 넘어간다. 물러나 있으면 통로에
/// 실제로 걸어 들어가야 넘어가므로 "나간다"가 스스로 고른 행동이 된다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ExitDoor : MonoBehaviour
{
    [SerializeField] private bool startOpen;

    [Tooltip("열렸을 때 판정이 통로 안쪽으로 물러나는 거리(칸). 0이면 벽 구멍 자리에 그대로 " +
             "있어 입구를 스치는 것만으로 넘어간다. 구름(RoomGates)이 통로를 12칸 덮으므로 " +
             "이 값이 그 안이면 물러난 문이 화면 밖으로 나가지 않는다.")]
    [SerializeField, Min(0f)] private float openDepth = 3f;

    public bool IsOpen { get; private set; }

    private Collider2D doorCollider;
    private bool used;

    /// <summary>닫혔을 때 제자리. 열고 닫을 때마다 여기를 기준으로 밀고 되돌린다.</summary>
    private Vector2 closedOffset;

    /// <summary>통로가 뻗어 나가는 쪽(+1 오른쪽 / −1 왼쪽). 문의 자리로 판별한다.</summary>
    private float outward;

    private void Awake()
    {
        // 예전에는 여기 달린 그림이 닫힘(갈색)/열림(연두색) 기둥으로 상태를 보여 줬다.
        // 지금은 통로를 메운 구름(CorridorCloud)이 그 일을 하므로 그림은 끈다 —
        // 구름이 걷히는 것이 곧 "나가도 된다"라, 기둥까지 서 있으면 신호가 둘이 된다.
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;

        doorCollider = GetComponent<Collider2D>();
        closedOffset = doorCollider.offset;
        outward = transform.localPosition.x >= 0f ? 1f : -1f;
        SetOpen(startOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        // 열려도 트리거로 바꾸지 않는다. 벽에 구멍이 생기면 안 된다.
        doorCollider.isTrigger = false;

        // 판정만 통로 안쪽으로 물린다. 오브젝트를 옮기지 않고 콜라이더 오프셋을 쓰는 이유는
        // 스물한 방의 프리팹이 이 자리를 벽 구멍에 맞춰 두고 있어서다 — 자리는 그대로 두고
        // 몸만 움직인다. 오프셋은 로컬 단위라 스케일(0.5)로 나눠 준다.
        float scale = Mathf.Abs(transform.lossyScale.x);
        float local = open && scale > 0.0001f ? openDepth / scale : 0f;
        doorCollider.offset = closedOffset + new Vector2(outward * local, 0f);
    }

    // 열린 뒤에 닿아도(Enter), 열리는 순간 이미 닿아 있었어도(Stay) 모두 처리한다.
    private void OnCollisionEnter2D(Collision2D collision) => TryPass(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => TryPass(collision.collider);

    private void TryPass(Collider2D other)
    {
        if (used || !IsOpen) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        used = true;
        if (RoomFlowController.Instance != null)
        {
            // 방을 바로 갈아 끼우지 않고 연출에 맡긴다 — 화면을 덮고, 다음 방을 올린 뒤,
            // 왼쪽 통로에서 걸어 들어오게 한다. 연출이 NextRoom을 부른다.
            RoomTransition.Ensure().Go();
        }
        else
        {
            // 방 흐름 관리자가 없으면 단계 1 방식으로 같은 씬을 다시 로드
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
