/// <summary>
/// 플레이어가 쓰는 기술. 값은 저장 순서를 겸하므로 중간에 끼워 넣지 말 것.
/// </summary>
public enum MoveType
{
    Tackle = 0,      // 몸통박치기 — 좌클릭 근접
    VineWhip = 1,    // 덩굴채찍 — 우클릭, 2칸 사거리
    SeedSow = 2,     // 씨뿌리기 — 스페이스바, 회복 장판
    PetalDance = 3,  // 꽃잎댄스 — 좌측 Shift, 피해 장판
}

/// <summary>기술의 이름·조작키 같은 표시용 정보. 로직은 <see cref="PlayerMoves"/>가 갖는다.</summary>
public static class MoveInfo
{
    /// <summary>배우는 순서. 처음 둘은 시작부터 갖고 있고, 진화할 때마다 하나씩 늘어난다.</summary>
    public static readonly MoveType[] LearnOrder =
    {
        MoveType.Tackle, MoveType.VineWhip, MoveType.SeedSow, MoveType.PetalDance,
    };

    /// <summary>시작할 때 이미 배운 기술 수.</summary>
    public const int StartingMoveCount = 2;

    /// <summary>기술 칸은 네 개까지다.</summary>
    public const int MaxMoves = 4;

    /// <summary>한 기술에 걸 수 있는 강화 횟수.</summary>
    public const int MaxUpgradesPerMove = 2;

    public static string NameOf(MoveType move)
    {
        switch (move)
        {
            case MoveType.Tackle: return "몸통박치기";
            case MoveType.VineWhip: return "덩굴채찍";
            case MoveType.SeedSow: return "씨뿌리기";
            case MoveType.PetalDance: return "꽃잎댄스";
        }
        return "?";
    }

    public static string KeyLabelOf(MoveType move)
    {
        switch (move)
        {
            case MoveType.Tackle: return "좌클릭";
            case MoveType.VineWhip: return "우클릭";
            case MoveType.SeedSow: return "Space";
            case MoveType.PetalDance: return "Shift";
        }
        return "";
    }

    /// <summary>실제로 동작하는 기술인지. 네 기술 모두 구현됐다.</summary>
    public static bool IsImplemented(MoveType move) => true;
}
