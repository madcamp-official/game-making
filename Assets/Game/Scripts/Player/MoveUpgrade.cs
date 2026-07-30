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
    SeedHeal = 6,        // 회복량 33% 증가 (6 → 8)
    SeedDuration = 7,    // 장판 지속시간 2초 증가
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

    // 장판 계열. 회복량과 지속시간은 비율이 아니라 고정값으로 더한다 —
    // 명세가 "33% 증가(실제로는 2 증가)"처럼 실제 더할 값을 못박아 두었다.
    public const int SeedHealStep = 2;         // 초당 회복 6 → 8
    public const float SeedDurationStep = 2f;  // 6초 → 8초
    public const float SeedRadiusStep = 1.15f; // 반지름 +15% (면적 약 +32%)
    public const float PetalRadiusStep = 1.15f; // 반지름 +15% (면적 약 +32%)
    public const float PetalDamageStep = 1.2f; // 피해 +20%
    public const float PetalCooldownStep = 0.85f; // 재사용 대기시간 -15%

    // 표기는 "무엇이 얼마만큼 늘거나 준다"로 통일한다 — 부호를 붙이지 않고 방향은 낱말로 적는다.
    //
    // 예전에는 부호와 낱말을 함께 썼는데("+20% 증가", "-20% 감소"), 한 화면에 나란히 놓으니
    // 같은 말을 두 번 하는 데다 "-20% 감소"는 깎이는 것인지 되돌아가는 것인지 오히려 헷갈렸다.
    // 낱말 자체도 갈렸다 — 같은 값을 몸통박치기는 "공격 속도 증가", 덩굴채찍은 "쿨타임 감소"라
    // 불러서 서로 다른 것을 건드리는 것처럼 읽혔다. 둘 다 "재사용 대기시간 감소"다.
    //
    // 유물 설명(RelicData.description)은 이 규칙을 따르지 않는다. 그쪽 문구는 따로 정해진 것이라
    // "+30%"처럼 부호를 붙여 적는다.
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
            "씨뿌리기", "회복량 33% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.SeedDuration, MoveType.SeedSow,
            "씨뿌리기", "장판 지속시간 2초 증가"),
        new MoveUpgradeOption(MoveUpgradeId.SeedRadius, MoveType.SeedSow,
            "씨뿌리기", "장판 크기 15% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalRadius, MoveType.PetalDance,
            "꽃잎댄스", "장판 크기 15% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalDamage, MoveType.PetalDance,
            "꽃잎댄스", "피해량 20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalCooldown, MoveType.PetalDance,
            "꽃잎댄스", "재사용 대기시간 15% 감소"),
    };
}
