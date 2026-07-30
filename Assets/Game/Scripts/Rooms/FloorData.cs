using UnityEngine;

/// <summary>
/// 한 층의 구성 데이터: 이름과 5개 방 프리팹 순서.
/// </summary>
[CreateAssetMenu(menuName = "Game/Floor", fileName = "NewFloor")]
public class FloorData : ScriptableObject
{
    public string floorName;
    public GameObject[] roomPrefabs;
    public string[] roomNames;

    /// <summary>
    /// 층의 배경음. 방 종류에 따라 넷 중 하나가 흐른다 (<see cref="RoomFlowController"/>).
    ///
    /// 곡을 방 프리팹이 아니라 층 데이터가 들고 있는 이유는, 한 층의 전투방 넷이 <b>같은 곡</b>을
    /// 나눠 쓰기 때문이다. 방마다 붙이면 같은 곡을 네 번 적어야 하고, 층의 색을 바꾸려면
    /// 일곱 방을 전부 열어야 한다.
    /// </summary>
    [Header("배경음")]
    [Tooltip("전투방에서 흐르는 곡. 층 안에서 방을 넘겨도 끊기지 않고 이어진다.")]
    public AudioClip battleBgm;

    [Tooltip("이벤트방.")]
    public AudioClip eventBgm;

    [Tooltip("상점.")]
    public AudioClip shopBgm;

    [Tooltip("보스방.")]
    public AudioClip bossBgm;

    /// <summary>
    /// 이 층 보스의 울음소리. 보스방에 들어설 때와 2막으로 넘어갈 때 운다.
    ///
    /// 보스 프리팹이 아니라 층이 들고 있다. 우는 자리가 둘인데 하나(방 입장)는 보스가 아직
    /// 자기 차례를 시작하기도 전이라, 보스에게 물어보려면 방을 뒤져 컨트롤러를 찾아야 한다 —
    /// 게다가 보스마다 컨트롤러가 다르다. 층이 곧 보스인 구조이므로 여기 두는 편이 짧다.
    /// </summary>
    [Tooltip("보스방 입장과 2막 전환에 울린다.")]
    public AudioClip bossCry;
}
