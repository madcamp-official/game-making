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

    [Tooltip("다음 층으로 넘어갈 때 비어 있는 체력 중 몇 할을 채울지. 1이면 완전 회복. " +
             "그 층에서 이미 진화로 회복했다면 이 회복은 건너뛴다 — 회복은 층당 한 번이다.")]
    [SerializeField, Range(0f, 1f)] private float floorHealMissingFraction = 0.45f;

    [Header("통로를 막는 구름")]
    [Tooltip("통로를 메우는 뭉게구름 그림(CorridorCloud.png). 비우면 구름 없이 예전처럼 동작한다.")]
    [SerializeField] private Sprite corridorCloudSprite;

    public int CurrentFloorIndex { get; private set; }
    public int CurrentRoomIndex { get; private set; } = -1;

    private GameObject currentRoom;
    private bool gameCleared;
    /// <summary>구름 그림이 비었다는 경고는 방마다 되풀이하지 않고 한 번만 남긴다.</summary>
    private bool warnedMissingCloud;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // GameFlow가 있으면 판이 언제 시작되는지는 그쪽이 정한다. 여기서 미리 방을 올리면
        // 타이틀 뒤에서 적이 움직이고 시간이 흘러, 판이 언제 시작됐는지가 흐려진다.
        if (GameFlow.Instance == null) BeginRun();
    }

    /// <summary>판을 처음부터 시작한다. 첫 층 첫 방을 올린다.</summary>
    public void BeginRun()
    {
        gameCleared = false;
        CurrentFloorIndex = 0;
        LoadRoom(0);
    }

    /// <summary>
    /// 다음 방으로 넘어간다. 상점방을 나가는 길이면 행복의알이 진화를 앞당긴다 —
    /// 상점 다음은 보스방이므로, 보스를 <b>만나기 전에</b> 한 단계 올라간 몸으로 들어가게 된다.
    /// </summary>
    public void NextRoom()
    {
        // 방 종류를 들고 있는 데이터가 없어서, 상점 관리자가 붙어 있느냐로 판별한다
        // (전투방 판별이 CombatRoomController의 유무를 보는 것과 같은 방식이다).
        // 방이 지워지기 전에 미리 봐 둬야 한다.
        bool leavingShop = currentRoom != null &&
                           currentRoom.GetComponentInChildren<ShopController>(true) != null;

        AdvanceRoom();

        if (leavingShop) TryHappyEggEvolve();
    }

    private void AdvanceRoom()
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
            bool healed = HealForNewFloor();
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage(
                    (CurrentFloorIndex + 1) + "층 — " + floors[CurrentFloorIndex].floorName + "에 도착했다!" +
                    (healed ? " 체력을 조금 회복했다." : ""), 2.5f);
            LoadRoom(0);
            return;
        }
        LoadRoom(CurrentRoomIndex + 1);
    }

    /// <summary>
    /// 층을 넘어가며 모자란 체력의 일부를 채운다 (완전 회복이 아니다).
    /// 실제로 회복했으면 참 — 도착 알림에 "회복했다"를 넣을지 정하는 데 쓴다.
    ///
    /// <b>이 층에서 이미 진화로 회복했다면 건너뛴다.</b> 층의 마지막 방이 보스방이고 보스를
    /// 잡으면 진화하므로, 그냥 두면 두 회복이 잇달아 터진다 — 각각 빈 체력의 절반 가까이라
    /// 합치면 대부분이 채워져, 보스에게 아무리 두들겨 맞아도 다음 층은 늘 만신창이가 아닌
    /// 몸으로 시작하게 된다. 회복은 층당 한 번이면 족하다.
    ///
    /// 진화가 없었던 층(마지막 단계에 이미 도달한 경우)에는 여기가 그 한 번을 맡는다.
    /// </summary>
    private bool HealForNewFloor()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return false;

        PlayerEvolution evolution = player.GetComponent<PlayerEvolution>();
        bool alreadyHealed = evolution != null && evolution.HealedThisFloor;
        if (evolution != null) evolution.NotifyFloorChanged();
        if (alreadyHealed) return false;

        Health health = player.GetComponent<Health>();
        if (health == null || health.IsDead) return false;

        int before = health.CurrentHealth;
        health.HealMissingFraction(floorHealMissingFraction);
        return health.CurrentHealth > before;
    }

    /// <summary>
    /// 행복의알: 상점방을 나갈 때 미리 진화한다.
    ///
    /// 보스 처치 후 진화는 그대로 남겨 둔다. <see cref="PlayerEvolution.Evolve"/>에 "층당 한 단계"
    /// 제한이 있어서, 여기서 이미 올라갔다면 같은 층 보스를 잡아도 두 번 진화하지 않는다.
    /// 즉 이 유물이 주는 것은 단계가 아니라 <b>순서</b>다.
    /// </summary>
    private void TryHappyEggEvolve()
    {
        if (gameCleared) return;
        if (RelicManager.Instance == null || !RelicManager.Instance.Has(RelicEffect.HappyEgg)) return;

        PlayerEvolution evolution = FindAnyObjectByType<PlayerEvolution>();
        if (evolution != null) evolution.Evolve();
    }

    /// <summary>개발용: 층 수. <see cref="DevHackPanel"/>에서만 쓰며, 개발이 끝나면 같이 지운다.</summary>
    public int FloorCount => floors != null ? floors.Length : 0;

    /// <summary>개발용: 그 층의 방 수.</summary>
    public int RoomCount(int floorIndex) =>
        floors != null && floorIndex >= 0 && floorIndex < floors.Length
            ? floors[floorIndex].roomPrefabs.Length : 0;

    /// <summary>
    /// 개발용: 방 종류를 영문으로. 치트 패널이 IMGUI 기본 폰트를 쓰는데 한글 글리프가 없어서,
    /// `roomNames`(한글) 대신 프리팹 이름의 뒷부분("F2Room3_Event" → "Event")을 쓴다.
    /// </summary>
    public string RoomKindLabel(int floorIndex, int roomIndex)
    {
        if (RoomCount(floorIndex) <= roomIndex || roomIndex < 0) return "?";
        string name = floors[floorIndex].roomPrefabs[roomIndex].name;
        int split = name.LastIndexOf('_');
        return split >= 0 && split + 1 < name.Length ? name.Substring(split + 1) : name;
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
        // 플레이어 장판(씨뿌리기·꽃잎댄스)은 씬 루트에 있어 방을 지워도 남는다. 같이 걷어 낸다.
        MoveZone.ClearAll();
        FloorData floor = floors[CurrentFloorIndex];
        CurrentRoomIndex = index;
        currentRoom = Instantiate(floor.roomPrefabs[index]);

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = playerSpawn;

        // 양쪽 통로를 막는 구름. 방마다 프리팹에 심지 않고 여기서 세운다 —
        // 스물한 방의 통로 자리를 전부 같게 맞춰 두었으므로 자리를 계산하는 편이 안전하다.
        //
        // ⚠️ 그림이 비어 있으면 구름이 <b>한 방에도 서지 않는다</b>. 통로가 통째로 뚫려
        // 싸움 도중에도 다음 방으로 걸어 나갈 수 있게 되는데, 조용히 넘어가면 왜 그런지
        // 알아낼 실마리가 없다. 실제로 한 번 겪었다 — 참조가 끊긴 줄 모르고 구름 코드를
        // 뒤졌다. 없으면 없다고 말하게 한다.
        if (corridorCloudSprite != null)
        {
            RoomGates.Create(currentRoom.transform, corridorCloudSprite);
        }
        else if (!warnedMissingCloud)
        {
            warnedMissingCloud = true;
            Debug.LogWarning("[방] corridorCloudSprite가 비어 있다 — 통로를 막는 구름이 서지 않는다. " +
                             "Gameplay 씬의 RoomFlowController에 CorridorCloud.png를 연결할 것.", this);
        }

        RunStats.ReachedRoom(CurrentFloorIndex, index);

        string label = floor.roomNames != null && index < floor.roomNames.Length ? floor.roomNames[index] : floor.roomPrefabs[index].name;
        if (UIManager.Instance != null)
            UIManager.Instance.SetRoomName(string.Format("{0}층 {1}  {2}/{3}  {4}",
                CurrentFloorIndex + 1, floor.floorName, index + 1, floor.roomPrefabs.Length, label));
    }

    private void GameClear()
    {
        gameCleared = true;
        // 결과 화면이 있으면 그쪽이 마무리를 맡는다.
        if (GameFlow.Instance != null) { GameFlow.Instance.FinishRun(true); return; }
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
