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
    [Tooltip("그림이 없을 때 쓰는 색. 두 스프라이트를 채우면 색 대신 그림이 바뀐다.")]
    [SerializeField] private Color closedColor = new Color(0.35f, 0.2f, 0.2f);
    [SerializeField] private Color openColor = new Color(0.3f, 0.9f, 0.4f);
    [Tooltip("길을 막은 모습(넘어진 통나무 등). 비우면 색만 바뀐다.")]
    [SerializeField] private Sprite closedSprite;
    [Tooltip("길이 열린 모습. 막은 것이 부서져 틈이 보이는 그림.")]
    [SerializeField] private Sprite openSprite;
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
        if (spriteRenderer == null) return;

        // 그림이 준비된 방은 막은 것이 부서지는 모습으로 보여 준다. 충돌체는 그대로 단단하지만
        // 틈이 보이므로 "들어가도 된다"가 읽힌다. 그림이 없는 방은 예전처럼 색만 바꾼다.
        if (closedSprite != null && openSprite != null)
        {
            spriteRenderer.sprite = open ? openSprite : closedSprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            spriteRenderer.color = open ? openColor : closedColor;
        }
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
