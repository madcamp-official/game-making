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

/// <summary>
/// 기술의 공통 규칙과 현재 기술 세트의 표시 정보를 읽는 창구.
/// 이름·설명·속성은 <see cref="PlayerMoveSet"/>에 있고, 조작키만 네 슬롯에 고정돼 있다.
/// </summary>
public static class MoveInfo
{
    /// <summary>기술 칸은 네 개까지다.</summary>
    public const int MaxMoves = 4;

    /// <summary>한 기술에 걸 수 있는 강화 횟수.</summary>
    public const int MaxUpgradesPerMove = 2;

    private static PlayerMoveSet CurrentSet =>
        PlayerMoves.Instance != null ? PlayerMoves.Instance.MoveSet : null;

    public static string NameOf(MoveType move, PlayerMoveSet set = null)
    {
        PlayerMoveDefinition definition = (set ?? CurrentSet)?.Find(move);
        return definition != null && !string.IsNullOrEmpty(definition.displayName)
            ? definition.displayName
            : move.ToString();
    }

    /// <summary>입력은 캐릭터가 달라도 같은 네 슬롯을 쓴다.</summary>
    public static string KeyLabelForSlot(int slot)
    {
        switch (slot)
        {
            case 0: return "좌클릭";
            case 1: return "우클릭";
            case 2: return "Shift";
            case 3: return "Space";
        }
        return "";
    }

    public static string KeyLabelOf(MoveType move, PlayerMoveSet set = null) =>
        KeyLabelForSlot((set ?? CurrentSet)?.IndexOf(move) ?? -1);

    /// <summary>
    /// 기술 한 줄 소개. 새로 배웠을 때 전용 화면(<see cref="BossRewardSequence"/>)이 보여 준다.
    ///
    /// <b>효과만 담백하게 적는다.</b> 어떻게 쓰면 좋은지, 무엇이 관건인지 같은 훈수는 넣지 않는다.
    /// 수치도 적지 않는다 — 진화 단계·강화·유물로 계속 달라져서, 어느 한쪽만 고치면
    /// 화면이 거짓말을 하게 된다.
    /// </summary>
    public static string SummaryOf(MoveType move, PlayerMoveSet set = null)
    {
        PlayerMoveDefinition definition = (set ?? CurrentSet)?.Find(move);
        return definition != null ? definition.summary ?? "" : "";
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
    public static AttackKind KindOf(MoveType move, PlayerMoveSet set = null)
    {
        PlayerMoveDefinition definition = (set ?? CurrentSet)?.Find(move);
        return definition != null ? definition.attackKind : AttackKind.None;
    }

    /// <summary>
    /// 기술 칸에 적는 꼬리표. 속성이 기본이고, 속성만으로 설명되지 않는 규칙은 뒤에 덧붙인다.
    /// </summary>
    public static string TagOf(MoveType move, PlayerMoveSet set = null)
    {
        PlayerMoveDefinition definition = (set ?? CurrentSet)?.Find(move);
        if (definition == null) return "";
        return !string.IsNullOrEmpty(definition.tagOverride)
            ? definition.tagOverride
            : AttackKinds.LabelOf(definition.attackKind);
    }

    /// <summary>현재 기술 세트에 들어 있는 기술인지.</summary>
    public static bool IsImplemented(MoveType move, PlayerMoveSet set = null) =>
        (set ?? CurrentSet)?.Find(move) != null;
}
