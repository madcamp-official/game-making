using UnityEngine;

/// <summary>
/// 한 층의 방 순서를 관리한다. 방 프리팹을 교체하며 플레이어를 입구로 옮긴다.
/// 마지막 방(보스방) 출구를 통과하면 층 클리어 처리를 한다.
/// </summary>
public class RoomFlowController : MonoBehaviour
{
    public static RoomFlowController Instance { get; private set; }

    [SerializeField] private GameObject[] roomPrefabs;
    [SerializeField] private string[] roomNames;
    [SerializeField] private Vector2 playerSpawn = new Vector2(-7f, 0f);

    public int CurrentRoomIndex { get; private set; } = -1;

    private GameObject currentRoom;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        LoadRoom(0);
    }

    public void NextRoom()
    {
        if (CurrentRoomIndex + 1 >= roomPrefabs.Length)
        {
            FloorClear();
            return;
        }
        LoadRoom(CurrentRoomIndex + 1);
    }

    private void LoadRoom(int index)
    {
        if (currentRoom != null) Destroy(currentRoom);
        CurrentRoomIndex = index;
        currentRoom = Instantiate(roomPrefabs[index]);

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = playerSpawn;

        string label = roomNames != null && index < roomNames.Length ? roomNames[index] : roomPrefabs[index].name;
        if (UIManager.Instance != null)
            UIManager.Instance.SetRoomName(string.Format("1층  {0}/{1}  {2}", index + 1, roomPrefabs.Length, label));
    }

    private void FloorClear()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.ControlEnabled = false;
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage("1층 클리어!  (2층은 추후 구현)", 9999f);
    }
}
