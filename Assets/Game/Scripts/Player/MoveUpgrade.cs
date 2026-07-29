/// <summary>
/// 기술 강화 선택지. 값은 "이미 고른 것"을 기억하는 데 쓰이므로 중간에 끼워 넣지 말 것.
/// </summary>
public enum MoveUpgradeId
{
    TackleDamage = 0,    // 피해량 +20%
    TackleSlow = 1,      // 공격 중 이속 감소량 -20%
    TackleSpeed = 2,     // 공격 속도 +10% (쿨타임 감소)
    VineRange = 3,       // 사거리 +20%
    VineStun = 4,        // 공격 후 경직 -20%
    VineCooldown = 5,    // 쿨타임 -20%
    SeedHeal = 6,        // 회복량 +2 (6 → 8)
    SeedDuration = 7,    // 지속시간 +2초
    SeedRadius = 8,      // 장판 크기 +30%
    PetalRadius = 9,     // 장판 크기 +20%
    PetalDamage = 10,    // 피해량 +20%
    PetalDuration = 11,  // 지속시간 +1초
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
    public const float DamageStep = 1.2f;      // 피해량 +20%
    public const float SlowStep = 0.8f;        // 이속 "감소량"을 20% 줄인다
    public const float SpeedStep = 0.9f;       // 공격 쿨타임 -10% = 공격 속도 +10%
    public const float RangeStep = 1.2f;       // 사거리 +20%
    public const float StunStep = 0.8f;        // 경직 -20%
    public const float CooldownStep = 0.8f;    // 쿨타임 -20%

    // 장판 계열. 회복량과 지속시간은 비율이 아니라 고정값으로 더한다 —
    // 명세가 "33% 증가(실제로는 2 증가)"처럼 실제 더할 값을 못박아 두었다.
    public const int SeedHealStep = 2;         // 초당 회복 6 → 8
    public const float SeedDurationStep = 2f;  // 5초 → 7초
    public const float SeedRadiusStep = 1.3f;  // 크기 +30%
    public const float PetalRadiusStep = 1.2f; // 크기 +20%
    public const float PetalDamageStep = 1.2f; // 피해 +20%
    public const float PetalDurationStep = 1f; // 2.5초 → 3.5초

    // 증감폭은 앞에 부호를 붙여 적는다. "20% 감소"는 20%가 되는 것인지 20%만큼 깎이는 것인지
    // 읽는 사람마다 갈리는데, "-20%"는 갈리지 않는다. 유물 설명도 같은 규칙을 쓴다.
    public static readonly MoveUpgradeOption[] All =
    {
        new MoveUpgradeOption(MoveUpgradeId.TackleDamage, MoveType.Tackle,
            "몸통박치기", "피해량 +20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.TackleSlow, MoveType.Tackle,
            "몸통박치기", "공격 중 이속 감소량 -20% 감소"),
        new MoveUpgradeOption(MoveUpgradeId.TackleSpeed, MoveType.Tackle,
            "몸통박치기", "공격 속도 +10% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.VineRange, MoveType.VineWhip,
            "덩굴채찍", "길이 +20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.VineStun, MoveType.VineWhip,
            "덩굴채찍", "공격 후 경직 -20% 감소"),
        new MoveUpgradeOption(MoveUpgradeId.VineCooldown, MoveType.VineWhip,
            "덩굴채찍", "쿨타임 -20% 감소"),
        new MoveUpgradeOption(MoveUpgradeId.SeedHeal, MoveType.SeedSow,
            "씨뿌리기", "회복량 +33% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.SeedDuration, MoveType.SeedSow,
            "씨뿌리기", "장판 지속시간 +2초 증가"),
        new MoveUpgradeOption(MoveUpgradeId.SeedRadius, MoveType.SeedSow,
            "씨뿌리기", "장판 크기 +30% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalRadius, MoveType.PetalDance,
            "꽃잎댄스", "장판 크기 +20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalDamage, MoveType.PetalDance,
            "꽃잎댄스", "피해량 +20% 증가"),
        new MoveUpgradeOption(MoveUpgradeId.PetalDuration, MoveType.PetalDance,
            "꽃잎댄스", "장판 지속시간 +1초 증가"),
    };
}
