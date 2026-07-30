using UnityEngine;

/// <summary>
/// 한 판(런) 동안의 기록. 결과 화면이 보여 줄 것들을 한곳에 모은다 —
/// 플레이 시간, 처치 수, 획득 골드 총합, 도달한 층·방, 고른 캐릭터.
///
/// 정적 클래스인 이유: 기록을 남기는 곳이 씬 곳곳에 흩어져 있다(적이 죽는 자리, 골드가
/// 들어오는 자리, 방이 바뀌는 자리). 그때마다 관리자 오브젝트를 찾아 물려 두는 것보다
/// 한 줄로 알리는 편이 끊길 여지가 없다. 판이 바뀌면 <see cref="Begin"/>이 전부 비운다.
///
/// 시간은 <see cref="Time.unscaledTime"/>으로 잰다. 대사창·유물 팝업·진화 컷씬이
/// <see cref="Time.timeScale"/>을 0으로 세우는데, 그 시간도 플레이한 시간이다.
/// </summary>
public static class RunStats
{
    /// <summary>이 판에서 고른 캐릭터. 결과 화면이 최종 진화 모습을 보여 줄 때 쓴다.</summary>
    public static CharacterData Character { get; private set; }

    public static int Kills { get; private set; }

    /// <summary>번 골드의 총합. 쓴 돈은 빼지 않는다 — 얼마를 벌었는지가 성적이다.</summary>
    public static int GoldEarned { get; private set; }

    /// <summary>도달한 가장 깊은 층·방 (0부터). 결과 화면은 1을 더해 보여 준다.</summary>
    public static int DeepestFloor { get; private set; }
    public static int DeepestRoom { get; private set; }

    /// <summary>
    /// 도달한 진화 단계 (0부터). 결과 화면이 <b>그때 그 모습의</b> 표정 초상을 고르는 데 쓴다 —
    /// 이상해꽃으로 쓰러졌으면 이상해씨가 아니라 이상해꽃의 얼굴이 떠야 한다.
    ///
    /// 플레이어 오브젝트에게 직접 묻지 않는 이유: 결과 화면은 쓰러진 <b>뒤에</b> 세워지는데
    /// 그때 플레이어가 아직 살아 있으리라는 보장이 없다. 단계가 바뀌는 순간 여기 적어 두면
    /// 화면은 기록만 읽으면 된다.
    /// </summary>
    public static int StageIndex { get; private set; }

    /// <summary>판이 끝났는지. 끝난 뒤에는 시간이 더 흐르지 않는다.</summary>
    public static bool Finished { get; private set; }

    private static float startedAt;
    private static float finishedAt;

    /// <summary>흐른 플레이 시간(초). 판이 끝났으면 끝난 시각에서 멈춘다.</summary>
    public static float ElapsedSeconds =>
        Mathf.Max(0f, (Finished ? finishedAt : Time.unscaledTime) - startedAt);

    /// <summary>"12분 34초" 꼴로. 한 시간을 넘기면 시간까지 붙인다.</summary>
    public static string ElapsedText
    {
        get
        {
            int total = Mathf.FloorToInt(ElapsedSeconds);
            int hours = total / 3600;
            int minutes = total % 3600 / 60;
            int seconds = total % 60;
            return hours > 0
                ? hours + "시간 " + minutes + "분 " + seconds + "초"
                : minutes + "분 " + seconds + "초";
        }
    }

    /// <summary>새 판을 시작한다. 이전 기록을 전부 비운다.</summary>
    public static void Begin(CharacterData character)
    {
        Character = character;
        Kills = 0;
        GoldEarned = 0;
        DeepestFloor = 0;
        DeepestRoom = 0;
        StageIndex = 0;
        Finished = false;
        startedAt = Time.unscaledTime;
        finishedAt = 0f;
    }

    /// <summary>판이 끝났다. 시간을 여기서 멈춘다.</summary>
    public static void Finish()
    {
        if (Finished) return;
        Finished = true;
        finishedAt = Time.unscaledTime;
    }

    public static void CountKill() => Kills++;

    /// <summary>
    /// 진화 단계가 정해질 때마다 알린다. <b>가장 높은 단계</b>를 남긴다 — 판을 시작할 때
    /// 1단계를 입히는 것도 이 문을 지나므로, 그냥 덮어쓰면 되돌아가 버린다.
    /// </summary>
    public static void ReachedStage(int index)
    {
        if (index > StageIndex) StageIndex = index;
    }

    public static void CountGold(int amount)
    {
        if (amount > 0) GoldEarned += amount;
    }

    /// <summary>
    /// 방에 들어설 때마다 알린다. 되돌아가는 길은 없지만, 층이 올라가면 방 번호가 0으로
    /// 돌아가므로 <b>가장 깊은 곳</b>을 따로 기억해야 한다.
    /// </summary>
    public static void ReachedRoom(int floorIndex, int roomIndex)
    {
        if (floorIndex > DeepestFloor || (floorIndex == DeepestFloor && roomIndex > DeepestRoom))
        {
            DeepestFloor = floorIndex;
            DeepestRoom = roomIndex;
        }
    }
}
