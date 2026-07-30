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
        // 첫 방만은 걸어 들어오는 연출이 없다 — 판이 시작되면 이미 방 안에 서 있다.
        // 그래서 방을 올리는 것이 곧 들어서는 것이고, 시작 글씨도 여기서 띄워야 한다.
        OnPlayerEnteredRoom();
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

        // 방을 갈아 끼우기 <b>전에</b> 진화한다. 예전에는 뒤에 두었는데, 그러면 진화 연출이
        // 보스방에 걸어 들어오는 연출과 겹쳤다. 진화 중에는 몸이 Kinematic이라 벽에 막히지
        // 않고 시간도 멈춰 있어서(timeScale 0), 걸어 들어오는 도중에 그대로 방 밖으로
        // 흘러 나가 맵 바깥에 서 있는 판이 나왔다.
        if (leavingShop) TryHappyEggEvolve();

        AdvanceRoom();
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

        // 지난 방에서 싸움이 끝나 낮춰 둔 음악을 제 크기로 되돌린다. 곡이 같은 층 안에서는
        // 이어지므로(전투방 → 전투방) 여기서 크기를 그 자리에서 올리면 문턱을 넘는 순간
        // 소리가 툭 튄다. 잦아들 때와 같은 속도로 도로 차오르게 둔다.
        GameAudio.SetBgmDuck(1f);
        GameAudio.PlayBgm(BgmFor(floor, index));

        RunStats.ReachedRoom(CurrentFloorIndex, index);

        string label = floor.roomNames != null && index < floor.roomNames.Length ? floor.roomNames[index] : floor.roomPrefabs[index].name;
        if (UIManager.Instance != null)
            UIManager.Instance.SetRoomName(string.Format("{0}층 {1}  {2}/{3}  {4}",
                CurrentFloorIndex + 1, floor.floorName, index + 1, floor.roomPrefabs.Length, label));
    }

    /// <summary>
    /// 이 방에 흐를 곡. 방 종류는 프리팹 이름 뒤에 붙은 꼬리("F2Room3_Event" → Event)로 읽는다.
    ///
    /// 방 종류를 담은 데이터가 따로 없어서 다른 곳에서는 컴포넌트 유무로 종류를 알아냈지만
    /// (<see cref="NextRoom"/>의 상점 판별), 보스방에는 그렇게 잡아낼 공통 조각이 없다 —
    /// 보스마다 컨트롤러가 다르다. 스물한 방이 모두 같은 규칙으로 이름 붙어 있으므로 이름을 믿는다.
    /// 꼬리를 알아볼 수 없으면 전투곡으로 떨어진다 (전투방이 층의 절반을 넘는다).
    /// </summary>
    private static AudioClip BgmFor(FloorData floor, int index)
    {
        if (floor == null || floor.roomPrefabs == null || index < 0 || index >= floor.roomPrefabs.Length) return null;
        GameObject prefab = floor.roomPrefabs[index];
        string name = prefab != null ? prefab.name : "";

        if (name.EndsWith("_Boss")) return floor.bossBgm;
        if (name.EndsWith("_Shop")) return floor.shopBgm;
        if (name.EndsWith("_Event")) return floor.eventBgm;
        return floor.battleBgm;
    }

    /// <summary>
    /// 주인공이 방 안까지 걸어 들어와 멈췄다 (<see cref="RoomTransition"/>이 알려 준다).
    ///
    /// 방을 <b>올리는</b> 것과 방에 <b>들어서는</b> 것은 다른 순간이다. 방은 화면이 검게 덮인
    /// 아래에서 갈아 끼우고, 주인공은 그 뒤에 밝아진 화면에서 통로를 걸어 들어온다. 보스
    /// 울음소리를 방 올리는 자리(<see cref="LoadRoom"/>)에 두었더니 아직 통로에 서 있는데
    /// — 심지어 화면이 검을 때 — 울어서, 누가 우는지 보이지 않았다.
    /// </summary>
    public void OnPlayerEnteredRoom()
    {
        if (floors == null || CurrentFloorIndex < 0 || CurrentFloorIndex >= floors.Length) return;
        FloorData floor = floors[CurrentFloorIndex];

        if (IsBossRoom(floor, CurrentRoomIndex)) { PlayBossCry(); return; }

        // 전투방은 글씨로 시작을 알린다. 방을 정리했을 때 뜨는 "스테이지 클리어!"와 짝이다 —
        // 시작에도 마디가 있어야 방 하나가 한 판처럼 읽힌다.
        //
        // 보스방은 여기로 오지 않는다. 그쪽은 울음소리가 이미 시작을 알리고 있어서 글씨까지
        // 겹치면 둘 다 묻힌다 (마무리도 마찬가지로 갈라져 있다 — BossRewardSequence).
        if (IsCombatRoom(floor, CurrentRoomIndex)) AnnounceStageStart();
    }

    /// <summary>
    /// "스테이지 시작!"을 띄운다. 방을 정리했을 때와 같은 배너를 쓰므로 두 글씨의 자리와
    /// 사라지는 방식이 저절로 같다.
    /// </summary>
    private static void AnnounceStageStart()
    {
        StageClearBanner banner = UIManager.Instance != null ? UIManager.Instance.StageClear : null;
        if (banner != null) banner.Show("스테이지 시작!", StartBannerHold, StartBannerFade);
    }

    /// <summary>시작 글씨가 떠 있는 시간과 지워지는 시간. 클리어 글씨보다 짧다 —
    /// 이쪽은 숨 돌리는 자리가 아니라 알림이고, 그 사이에도 적은 이미 달려온다.</summary>
    private const float StartBannerHold = 0.6f;
    private const float StartBannerFade = 0.45f;

    private static bool IsBossRoom(FloorData floor, int index) => RoomNameEndsWith(floor, index, "_Boss");

    /// <summary>
    /// 보스방이 아닌 순수 전투방인지. 방 종류는 다른 곳과 같은 규칙으로 프리팹 이름 뒤에
    /// 붙은 꼬리로 읽는다 (<see cref="BgmFor"/> 참고) — 이벤트·상점·보스가 아니면 전투방이다.
    /// </summary>
    private static bool IsCombatRoom(FloorData floor, int index) =>
        !RoomNameEndsWith(floor, index, "_Boss") &&
        !RoomNameEndsWith(floor, index, "_Shop") &&
        !RoomNameEndsWith(floor, index, "_Event") &&
        RoomPrefabAt(floor, index) != null;

    private static GameObject RoomPrefabAt(FloorData floor, int index)
    {
        if (floor == null || floor.roomPrefabs == null || index < 0 || index >= floor.roomPrefabs.Length) return null;
        return floor.roomPrefabs[index];
    }

    private static bool RoomNameEndsWith(FloorData floor, int index, string suffix)
    {
        GameObject prefab = RoomPrefabAt(floor, index);
        return prefab != null && prefab.name.EndsWith(suffix);
    }

    /// <summary>
    /// 지금 층 보스의 울음소리를 낸다. 방에 들어설 때는 여기서, 2막으로 넘어갈 때는 보스
    /// 컨트롤러가 부른다.
    ///
    /// 세 보스가 각자 자기 소리를 들고 있게 하지 않은 이유는 <see cref="FloorData.bossCry"/>에
    /// 적어 두었다. 보스 쪽에서는 어느 층인지 따질 필요 없이 이 문만 두드리면 된다.
    /// </summary>
    public static void PlayBossCry()
    {
        RoomFlowController flow = Instance;
        if (flow == null || flow.floors == null) return;
        if (flow.CurrentFloorIndex < 0 || flow.CurrentFloorIndex >= flow.floors.Length) return;

        FloorData floor = flow.floors[flow.CurrentFloorIndex];
        if (floor != null) GameAudio.PlaySfx(floor.bossCry);
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
