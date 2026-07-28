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

    /// <summary>
    /// 기술의 사거리 속성. 유물·이벤트 배율이 여기서 갈린다 (<see cref="AttackKinds"/>).
    ///
    /// 덩굴채찍은 2칸 밖에서 닿으므로 원거리다 — 구애 시리즈에서 잎날가르기가 있던 자리를
    /// 그대로 이어받는다. 꽃잎댄스는 발밑 장판이라 몸으로 붙어야 하니 근접이고,
    /// 씨뿌리기는 피해가 없어 속성이 없다.
    /// </summary>
    public static AttackKind KindOf(MoveType move)
    {
        switch (move)
        {
            case MoveType.Tackle: return AttackKind.Melee;
            case MoveType.VineWhip: return AttackKind.Ranged;
            case MoveType.SeedSow: return AttackKind.None;
            case MoveType.PetalDance: return AttackKind.Melee;
        }
        return AttackKind.None;
    }

    /// <summary>
    /// 기술 칸에 적는 꼬리표. 속성이 기본이고, 속성만으로 설명되지 않는 규칙은 뒤에 덧붙인다.
    /// </summary>
    public static string TagOf(MoveType move)
    {
        // 씨뿌리기는 때리는 기술이 아니라 속성이 없다. 그 자리에 "방마다 한 번"이라는
        // 제약을 적는다 — 어두워진 칸만으로는 언제 다시 차는지 알 수 없다.
        if (move == MoveType.SeedSow) return "방당 1회";
        return AttackKinds.LabelOf(KindOf(move));
    }

    /// <summary>실제로 동작하는 기술인지. 네 기술 모두 구현됐다.</summary>
    public static bool IsImplemented(MoveType move) => true;
}
