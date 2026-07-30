/// <summary>
/// 플레이어가 쓰는 기술. 값은 저장 순서를 겸하므로 중간에 끼워 넣지 말 것.
/// </summary>
public enum MoveType
{
    // 이상해씨 계열
    Tackle = 0,      // 몸통박치기 — 좌클릭 근접
    VineWhip = 1,    // 덩굴채찍 — 우클릭, 2칸 사거리
    SeedSow = 2,     // 씨뿌리기 — 좌측 Shift, 회복 장판
    PetalDance = 3,  // 꽃잎댄스 — 스페이스바, 피해 장판

    // 파이리(리자몽) 계열
    FireSpit = 4,     // 불꽃세례 — 좌클릭 투사체
    DragonDance = 5,  // 용의춤 — 우클릭, 공격력·이동 속도 버프
    DragonClaw = 6,   // 드래곤클로 — Shift 근접
    Flamethrower = 7, // 화염방사 — 스페이스바, 이어지는 화염 줄기

    // 꼬부기(거북왕) 계열
    WaterGun = 8,        // 물대포 — 좌클릭 (판정은 몸통박치기와 같은 근접)
    Surf = 9,            // 파도타기 — 우클릭 돌진
    RocketHeadbutt = 10, // 로켓박치기 — Shift, 무적 돌진
    HydroPump = 11,      // 하이드로펌프 — 스페이스바, 자리 고정 물줄기
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
    /// 속성은 캐릭터 데이터(<see cref="PlayerMoveDefinition.attackKind"/>)에 적혀 있다.
    /// 덩굴채찍은 2칸 밖에서 닿으므로 원거리다 — 구애 시리즈에서 잎날가르기가 있던 자리를
    /// 그대로 이어받는다. 씨뿌리기·용의춤은 피해가 없어 속성이 없다.
    ///
    /// 꽃잎댄스는 <b>근접</b>이다. 한때 "물러나 있어도 때린다"는 이유로 원거리에 두었지만,
    /// 몸을 따라다니는 장판이라 결국 적에게 붙어 비비는 기술이고 — 리자몽 계열이 원거리
    /// 화력을 맡게 되면서, 이상해꽃은 근접·회복 쪽으로 몫을 갈랐다. 이상해씨 계열에서
    /// 원거리 배율(리보플라빈·구애안경)을 받는 것은 이제 덩굴채찍뿐이다.
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
