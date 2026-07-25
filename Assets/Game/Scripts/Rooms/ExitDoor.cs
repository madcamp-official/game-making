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
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ExitDoor : MonoBehaviour
{
    [SerializeField] private Color closedColor = new Color(0.35f, 0.2f, 0.2f);
    [SerializeField] private Color openColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private bool startOpen;

    public bool IsOpen { get; private set; }

    private SpriteRenderer spriteRenderer;
    private Collider2D doorCollider;
    private bool used;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        doorCollider = GetComponent<Collider2D>();
        SetOpen(startOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        // 열려도 트리거로 바꾸지 않는다. 벽에 구멍이 생기면 안 된다.
        doorCollider.isTrigger = false;
        if (spriteRenderer != null)
            spriteRenderer.color = open ? openColor : closedColor;
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
            RoomFlowController.Instance.NextRoom();
        }
        else
        {
            // 방 흐름 관리자가 없으면 단계 1 방식으로 같은 씬을 다시 로드
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
