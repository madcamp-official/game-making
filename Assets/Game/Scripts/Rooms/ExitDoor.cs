using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 방 출구. 벽의 빈 구간을 메우는 단단한 충돌체이며, 열린 뒤 플레이어가 <b>몸을 대고 밀면</b>
/// 다음 방으로 넘어간다.
///
/// 열릴 때 충돌체를 트리거로 바꾸지 않는다. 그러면 벽에 실제로 구멍이 뚫리고, 트리거 진입
/// 이벤트를 한 번이라도 놓치면(열리는 순간 이미 겹쳐 있었거나, 다음 방으로 넘어가지 않는
/// 게임 클리어 시점 등) 플레이어가 그대로 방 밖으로 걸어 나갈 수 있었다. <b>문은 늘 단단하다.</b>
/// 덕분에 돌아다닐 수 있는 범위가 정확히 방 네모(±7 × ±5)로 유지된다 — 통로는 그림이고,
/// 발을 들이는 곳이 아니다.
///
/// 그래서 "넘어간다"는 물리 충돌이 아니라 아래 두 조건으로 따로 판정한다.
///
/// <list type="number">
/// <item>문을 <b>마주 보고</b> 서 있을 것 (<see cref="passHalfHeight"/>)</item>
/// <item>그 자세로 <b>잠깐 버틸</b> 것 (<see cref="passDuration"/>)</item>
/// </list>
///
/// 닿기만 하면 넘어가던 시절에는 통로 입구를 스치는 것만으로 방이 넘어갔다. 통로 구멍이
/// 두 칸(y ±1)이라, 오른쪽 벽에 붙어 오르내리다 y 1.3쯤에서도 몸통 아래쪽이 문의 위 모서리에
/// 걸린다 — 나갈 생각이 없었는데 끌려 나갔다. 가운데 판정과 버티는 시간이 그 둘을 가른다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ExitDoor : MonoBehaviour
{
    [SerializeField] private bool startOpen;

    [Tooltip("문 한가운데에서 세로로 이 안에 들어와야 넘어간다. 통로 구멍이 두 칸(±1)이라 " +
             "1로 두면 벽을 타고 내려오다 모서리에 몸끝만 스쳐도 넘어간다.")]
    [SerializeField, Min(0f)] private float passHalfHeight = 0.5f;

    [Tooltip("문에 몸을 댄 채 이만큼 버텨야 넘어간다. 0이면 닿는 순간이다. " +
             "걸어 나갈 때는 계속 닿아 있으므로 멈칫하는 느낌이 나지 않는다.")]
    [SerializeField, Min(0f)] private float passDuration = 0.25f;

    public bool IsOpen { get; private set; }

    /// <summary>몸이 닿았다고 볼 틈. 물리 엔진이 두 몸을 아주 살짝 띄워 두므로 0으로 잴 수 없다.</summary>
    private const float ContactSlack = 0.06f;

    private Collider2D doorCollider;
    private PlayerController player;
    private Collider2D playerCollider;
    private bool used;

    /// <summary>문을 밀기 시작한 때. 떨어지면 −1로 되돌아가 처음부터 다시 센다.</summary>
    private float pressingSince = -1f;

    private void Awake()
    {
        // 예전에는 여기 달린 그림이 닫힘(갈색)/열림(연두색) 기둥으로 상태를 보여 줬다.
        // 지금은 통로를 메운 구름(CorridorCloud)이 그 일을 하므로 그림은 끈다 —
        // 구름이 걷히는 것이 곧 "나가도 된다"라, 기둥까지 서 있으면 신호가 둘이 된다.
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;

        doorCollider = GetComponent<Collider2D>();
        SetOpen(startOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        // 열려도 트리거로 바꾸지 않는다. 벽에 구멍이 생기면 안 된다.
        doorCollider.isTrigger = false;
        pressingSince = -1f;
    }

    /// <summary>
    /// 충돌 이벤트가 아니라 매 프레임 거리로 잰다. 이벤트는 "닿았다/떨어졌다"만 알려 주는데,
    /// 여기서 알아야 하는 것은 <b>얼마나 오래 대고 있었나</b>라 상태를 직접 보는 편이 맞다.
    /// 열리는 순간 이미 겹쳐 있는 경우도 저절로 처리된다.
    /// </summary>
    private void Update()
    {
        if (used || !IsOpen) return;

        if (!PlayerPressing())
        {
            pressingSince = -1f;
            return;
        }

        if (pressingSince < 0f) pressingSince = Time.time;
        if (Time.time - pressingSince < passDuration) return;
        Pass();
    }

    private bool PlayerPressing()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null) return false;
            playerCollider = player.GetComponent<Collider2D>();
        }

        // 걸어 들어오는 연출처럼 조작이 꺼져 있는 동안은 세지 않는다. 스스로 민 것이 아니다.
        if (!player.ControlEnabled) return false;
        if (playerCollider == null || !playerCollider.enabled) return false;

        // 문을 마주 보고 서 있는가. 모서리를 스치는 것과 여기서 갈린다.
        if (Mathf.Abs(player.transform.position.y - transform.position.y) > passHalfHeight) return false;

        ColliderDistance2D gap = doorCollider.Distance(playerCollider);
        return gap.isValid && gap.distance <= ContactSlack;
    }

    private void Pass()
    {
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
