using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 층과 방 순서를 관리한다. FloorData 목록을 따라 방 프리팹을 교체하고,
/// 각 층의 마지막 방(보스방)을 나가면 다음 층으로, 마지막 층이면 게임 클리어.
/// </summary>
public class RoomFlowController : MonoBehaviour
{
    public static RoomFlowController Instance { get; private set; }

    [SerializeField] private FloorData[] floors;
    [SerializeField] private Vector2 playerSpawn = new Vector2(-7f, 0f);

    public int CurrentFloorIndex { get; private set; }
    public int CurrentRoomIndex { get; private set; } = -1;

    private GameObject currentRoom;
    private bool gameCleared;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CurrentFloorIndex = 0;
        LoadRoom(0);
    }

    public void NextRoom()
    {
        FloorData floor = floors[CurrentFloorIndex];
        if (CurrentRoomIndex + 1 >= floor.roomPrefabs.Length)
        {
            // 층의 마지막 방을 통과
            if (CurrentFloorIndex + 1 >= floors.Length)
            {
                GameClear();
                return;
            }
            CurrentFloorIndex++;
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage((CurrentFloorIndex + 1) + "층 — " + floors[CurrentFloorIndex].floorName + "에 도착했다! 체력이 모두 회복되었다.", 2.5f);
            LoadRoom(0);

            // 층을 넘어가면 체력을 완전히 회복한다.
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                Health health = player.GetComponent<Health>();
                if (health != null) health.Heal(health.MaxHealth);
            }
            return;
        }
        LoadRoom(CurrentRoomIndex + 1);
    }

    /// <summary>
    /// 개발용: 임의의 층·방으로 바로 이동한다. <paramref name="roomIndex"/>가 음수면 그 층의 마지막 방(보스방).
    /// <see cref="DevHackPanel"/>에서만 쓰며, 개발이 끝나면 같이 지운다.
    /// </summary>
    public void WarpTo(int floorIndex, int roomIndex)
    {
        if (floors == null || floors.Length == 0) return;
        CurrentFloorIndex = Mathf.Clamp(floorIndex, 0, floors.Length - 1);
        int roomCount = floors[CurrentFloorIndex].roomPrefabs.Length;
        LoadRoom(roomIndex < 0 ? roomCount - 1 : Mathf.Clamp(roomIndex, 0, roomCount - 1));
    }

    private void LoadRoom(int index)
    {
        if (currentRoom != null) Destroy(currentRoom);
        FloorData floor = floors[CurrentFloorIndex];
        CurrentRoomIndex = index;
        currentRoom = Instantiate(floor.roomPrefabs[index]);

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = playerSpawn;

        string label = floor.roomNames != null && index < floor.roomNames.Length ? floor.roomNames[index] : floor.roomPrefabs[index].name;
        if (UIManager.Instance != null)
            UIManager.Instance.SetRoomName(string.Format("{0}층 {1}  {2}/{3}  {4}",
                CurrentFloorIndex + 1, floor.floorName, index + 1, floor.roomPrefabs.Length, label));
    }

    private void GameClear()
    {
        gameCleared = true;
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.ControlEnabled = false;
        if (UIManager.Instance != null)
        {
            int gold = RunManager.Instance != null ? RunManager.Instance.Gold : 0;
            UIManager.Instance.ShowMessage(
                "게임 클리어! 모든 층을 정복했다!  ·  최종 골드 " + gold + "G\nR : 다시 시작", 9999f);
        }
    }

    private void Update()
    {
        if (!gameCleared) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
