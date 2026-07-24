using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 방 출구. 닫혀 있으면 통과할 수 없고, 열리면 플레이어가 닿았을 때 다음 방을 로드한다.
/// 단계 1에서는 같은 테스트 방(현재 씬)을 다시 로드한다.
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

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        doorCollider = GetComponent<Collider2D>();
        SetOpen(startOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        // 열리면 트리거로 바꿔 플레이어가 진입할 수 있게 한다.
        doorCollider.isTrigger = open;
        if (spriteRenderer != null)
            spriteRenderer.color = open ? openColor : closedColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsOpen) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

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
