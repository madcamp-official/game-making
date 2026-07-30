/// <summary>
/// 기술 강화 선택지. 값은 "이미 고른 것"을 기억하는 데 쓰이므로 중간에 끼워 넣지 말 것.
/// </summary>
public enum MoveUpgradeId
{
    TackleDamage = 0,    // 피해량 20% 증가
    TackleRadius = 1,    // 공격 범위 15% 증가
    TackleSpeed = 2,     // 재사용 대기시간 15% 감소
    VineRange = 3,       // 길이 20% 증가
    VineSlowDuration = 4, // 감속 지속시간 50% 증가
    VineCooldown = 5,    // 재사용 대기시간 10% 감소
    SeedHeal = 6,        // 회복량 약 33% 증가 (6 → 8)
    SeedDuration = 7,    // 장판 지속시간 약 33% 증가 (6초 → 8초)
    SeedRadius = 8,      // 장판 크기 15% 증가
    PetalRadius = 9,     // 장판 크기 15% 증가
    PetalDamage = 10,    // 피해량 20% 증가
    PetalCooldown = 11,  // 재사용 대기시간 15% 감소
}

/// <summary>강화 선택지 하나의 표시 정보와 소속 기술.</summary>
public struct MoveUpgradeOption
{
    public readonly MoveUpgradeId id;
    public readonly MoveType move;
    public readonly string title;
    public readonly string detail;

    public MoveUpgradeOption(MoveUpgradeId id, MoveType move, string title, string detail)
    {
        this.id = id;
        this.move = move;
        this.title = title;
        this.detail = detail;
    }
}

public static class MoveUpgrades
{
    /// <summary>강화 한 번당 곱해지는 값. 여러 번 걸면 곱으로 쌓인다.</summary>
    public const float DamageStep = 1.2f;      // 피해량 20% 증가
    public const float TackleRadiusStep = 1.15f; // 공격 반지름 15% 증가
    public const float SpeedStep = 0.85f;      // 재사용 대기시간 15% 감소
    public const float RangeStep = 1.2f;       // 길이 20% 증가
    public const float VineSlowDurationStep = 1.5f; // 감속 지속시간 50% 증가
    public const float CooldownStep = 0.9f;    // 재사용 대기시간 10% 감소

    // 모든 강화는 기본 수치에 곱할 배율이다. 4/3 배율은 현재 6을 8로 만들면서도
    // 캐릭터별 기본 수치가 달라졌을 때 같은 비율로 강해지게 한다.
    public const float SeedHealStep = 4f / 3f;
    public const float SeedDurationStep = 4f / 3f;
    public const float SeedRadiusStep = 1.15f; // 반지름 15% 증가 (면적 약 32% 증가)
    public const float PetalRadiusStep = 1.15f; // 반지름 15% 증가 (면적 약 32% 증가)
    public const float PetalDamageStep = 1.2f; // 피해 20% 증가
    public const float PetalCooldownStep = 0.85f; // 재사용 대기시간 15% 감소

    // 표기는 "무엇이 얼마만큼 늘거나 준다"로 통일한다 — 부호를 붙이지 않고 방향은 낱말로 적는다.
    //
    // 예전에는 수치 앞의 부호와 "증가/감소"를 함께 써 같은 뜻을 두 번 표현했고,
    // 감소 앞의 음수 부호는 방향을 오히려 헷갈리게 했다.
    // 낱말 자체도 갈렸다 — 같은 값을 몸통박치기는 "공격 속도 증가", 덩굴채찍은 "쿨타임 감소"라
    // 불러서 서로 다른 것을 건드리는 것처럼 읽혔다. 둘 다 "재사용 대기시간 감소"다.
    //
    // 유물 설명도 같은 규칙을 쓴다.
    public static readonly MoveUpgradeOption[] All =
    {
        new MoveUpgradeOption(MoveUpgradeId.TackleDamage, MoveType.Tackle,
            "몸통박치기", "피해량 20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.TackleRadius, MoveType.Tackle,
            "몸통박치기", "공격 범위 15% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.TackleSpeed, MoveType.Tackle,
            "몸통박치기", "재사용 대기시간 15% 감소"),
        new MoveUpgradeOption(MoveUpgradeId.VineRange, MoveType.VineWhip,
            "덩굴채찍", "길이 20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.VineSlowDuration, MoveType.VineWhip,
            "덩굴채찍", "감속 지속시간 50% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.VineCooldown, MoveType.VineWhip,
            "덩굴채찍", "재사용 대기시간 10% 감소"),
        new MoveUpgradeOption(MoveUpgradeId.SeedHeal, MoveType.SeedSow,
            "씨뿌리기", "회복량 약 33% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.SeedDuration, MoveType.SeedSow,
            "씨뿌리기", "장판 지속시간 약 33% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.SeedRadius, MoveType.SeedSow,
            "씨뿌리기", "장판 크기 15% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalRadius, MoveType.PetalDance,
            "꽃잎댄스", "장판 크기 15% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalDamage, MoveType.PetalDance,
            "꽃잎댄스", "피해량 20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalCooldown, MoveType.PetalDance,
            "꽃잎댄스", "재사용 대기시간 15% 감소"),
    };

    /// <summary>
    /// ID로 강화 정의를 찾는다. 어떤 강화가 후보에 들어가는지는 캐릭터의
    /// <see cref="PlayerMoveSet"/>이 정하고, 이 카탈로그는 실제 수치와 문구만 제공한다.
    /// </summary>
    public static bool TryGet(MoveUpgradeId id, out MoveUpgradeOption option)
    {
        foreach (MoveUpgradeOption candidate in All)
        {
            if (candidate.id != id) continue;
            option = candidate;
            return true;
        }
        option = default;
        return false;
    }
}
