/// <summary>
/// 플레이어가 쓰는 기술. 값은 저장 순서를 겸하므로 중간에 끼워 넣지 말 것.
/// </summary>
public enum MoveType
{
    Tackle = 0,      // 몸통박치기 — 좌클릭 근접
    VineWhip = 1,    // 덩굴채찍 — 우클릭, 2칸 사거리
    SeedSow = 2,     // 씨뿌리기 — 좌측 Shift, 회복 장판
    PetalDance = 3,  // 꽃잎댄스 — 스페이스바, 피해 장판
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
            case MoveType.SeedSow: return "Shift";
            case MoveType.PetalDance: return "Space";
        }
        return "";
    }

    /// <summary>
    /// 기술 한 줄 소개. 새로 배웠을 때 전용 화면(<see cref="BossRewardSequence"/>)이 보여 준다.
    ///
    /// <b>효과만 담백하게 적는다.</b> 어떻게 쓰면 좋은지, 무엇이 관건인지 같은 훈수는 넣지 않는다.
    /// 수치도 적지 않는다 — 진화 단계·강화·유물로 계속 달라져서, 어느 한쪽만 고치면
    /// 화면이 거짓말을 하게 된다.
    /// </summary>
    public static string SummaryOf(MoveType move)
    {
        switch (move)
        {
            case MoveType.Tackle:
                return "겨눈 방향을 후려치는 근접 공격.";
            case MoveType.VineWhip:
                return "두어 칸 밖까지 채찍을 뻗어 그 선 위의 적을 때리고 밀쳐 낸다.\n" +
                       "휘두른 뒤에는 잠깐 움직일 수 없다.";
            case MoveType.SeedSow:
                return "발밑에 회복 장판을 깐다. 그 위에 서 있는 동안 체력이 차오른다.\n" +
                       "전투방마다 한 번 쓸 수 있다.";
            case MoveType.PetalDance:
                return "몸을 따라다니는 피해 장판을 만든다. 장판 안의 적이 주기적으로 피해를 입는다.";
        }
        return "";
    }

    /// <summary>
    /// 기술의 사거리 속성. 유물·이벤트 배율이 여기서 갈린다 (<see cref="AttackKinds"/>).
    ///
    /// 덩굴채찍은 2칸 밖에서 닿으므로 원거리다 — 구애 시리즈에서 잎날가르기가 있던 자리를
    /// 그대로 이어받는다. 꽃잎댄스도 원거리다: 발밑에 까는 장판이지만 깔아 두고 물러나 있어도
    /// 계속 때리므로, 몸을 붙여야 성립하는 몸통박치기와 같은 부류로 묶을 수 없다.
    /// 씨뿌리기는 피해가 없어 속성이 없다.
    ///
    /// 덩굴채찍이 견제기가 되어 피해가 낮아진 뒤로, <b>원거리 피해 배율(리보플라빈·구애안경)의
    /// 실질 대상은 꽃잎댄스</b>다. 셋 중 하나를 옮길 때는 이 균형부터 본다.
    /// </summary>
    public static AttackKind KindOf(MoveType move)
    {
        switch (move)
        {
            case MoveType.Tackle: return AttackKind.Melee;
            case MoveType.VineWhip: return AttackKind.Ranged;
            case MoveType.SeedSow: return AttackKind.None;
            case MoveType.PetalDance: return AttackKind.Ranged;
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
