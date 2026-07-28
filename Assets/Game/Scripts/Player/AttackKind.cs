/// <summary>
/// 공격의 사거리 속성. 포켓몬의 물리·특수처럼, 배율이 갈리는 기준이 된다.
///
/// 이 속성을 보는 곳:
/// * 유물 — 구애머리띠(근접 +50%/원거리 -50%), 구애안경(그 반대), 구애스카프(양쪽 -20%)
/// * 이벤트 — 2층 시라소몬(근접 +20%), 홍수몬(원거리 +20%)
///
/// 예전에는 공격마다 <c>RelicMultiplier(true/false)</c>를 직접 넘겼다. 그러면 "이 공격이
/// 근접인가"라는 답이 호출부마다 흩어져, 기술이 늘 때마다 빠뜨리기 쉬웠다. 속성을 기술에
/// 붙여 두면(<see cref="MoveInfo.KindOf"/>) 한 곳만 보면 된다.
/// </summary>
public enum AttackKind
{
    /// <summary>피해를 주지 않는 기술. 어떤 공격 배율도 타지 않는다.</summary>
    None = 0,
    /// <summary>몸으로 붙어야 닿는 공격.</summary>
    Melee = 1,
    /// <summary>거리를 두고 닿는 공격.</summary>
    Ranged = 2,
}

public static class AttackKinds
{
    /// <summary>기술 칸에 적는 이름. 속성이 없는 기술은 빈 문자열이다.</summary>
    public static string LabelOf(AttackKind kind)
    {
        switch (kind)
        {
            case AttackKind.Melee: return "근접";
            case AttackKind.Ranged: return "원거리";
        }
        return "";
    }

    /// <summary>
    /// 이 속성의 공격에 곱해지는 배율. 유물과 이벤트 강화를 함께 본다.
    /// 둘 다 없으면 1이다.
    /// </summary>
    public static float DamageMultiplier(AttackKind kind)
    {
        if (kind == AttackKind.None) return 1f;

        bool melee = kind == AttackKind.Melee;
        float multiplier = melee ? EventBuffs.Melee : EventBuffs.Ranged;

        RelicManager relics = RelicManager.Instance;
        if (relics == null) return multiplier;
        return multiplier * (melee ? relics.MeleeDamageMultiplier : relics.RangedDamageMultiplier);
    }
}
