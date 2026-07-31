using UnityEngine;

/// <summary>
/// 유물 효과 식별자. 값은 에셋에 숫자로 저장되므로 중간에 끼워 넣거나 순서를 바꾸면 안 된다.
/// 새 효과는 항상 맨 뒤에 추가한다.
/// </summary>
public enum RelicEffect
{
    HappyEgg = 0,     // 행복의알: 보스방 진입 전에 미리 진화
    EnergyRoot = 1,   // 기력의 덩어리: 기절 시 방 진입 전 상태로 부활 (1회 소비)
    AmuletCoin = 2,   // 부적금화: 골드 획득량 25% 증가
    ChoiceBand = 3,   // 구애머리띠: 근접 30% 증가, 원거리 50% 감소
    ChoiceSpecs = 4,  // 구애안경: 원거리 30% 증가, 근접 50% 감소
    ChoiceScarf = 5,  // 구애스카프: 이동 20% 증가, 근접·원거리 20% 감소
    BigRoot = 6,      // 큰뿌리: 회복량 40% 증가
    Leftovers = 7,    // 먹다남은음식: 전투방 클리어마다 체력 12 회복
    WideLens = 8,     // 광각렌즈: 모든 공격 크기 15% 증가
    ShellBell = 9,    // 조개껍질방울: 적 처치마다 체력 3 회복
    LifeOrb = 10,     // 생명의구슬: 최대 체력 30% 감소, 공격 피해 30% 증가
    // 11은 자뭉열매였다. 유물에서 빼고 상점 포션(최대 체력의 33% 회복)으로 옮겼으므로 다시 쓰지 않는다.
    // 12는 잉어킹의 비늘이었다. 잉어킹 이벤트와 함께 사라졌으므로 다시 쓰지 않는다.
    QuickClaw = 13,      // 선제공격손톱: 시간으로 도는 쿨타임 15% 감소
    LightClay = 14,      // 빛의점토: 장판 지속시간 30% 증가
    RareCandy = 15,      // 이상한사탕: 획득 즉시 기술 강화 선택지 1회
    RockyHelmet = 16,    // 울퉁불퉁멧: 전투 피해를 받으면 주변 적에게 반사 피해
    Nugget = 17,         // 금구슬: 획득 즉시 골드
    TechMachine = 18,    // 기술머신: 강화 선택지 3개 → 4개
    DowsingMachine = 19, // 다우징머신: 보스 보상 유물을 둘 중 하나 고른다
    HpUp = 20,           // 맥스업: 최대 체력 15% 증가
    Protein = 21,        // 타우린: 근접 피해 15% 증가
    Calcium = 22,        // 리보플라빈: 원거리 피해 15% 증가
    Carbos = 23,         // 알칼로이드: 이동 속도 10% 증가
}

/// <summary>
/// 유물 희귀도. 상점 가격이 여기서 갈린다 (<see cref="RelicManager.PriceOf"/>).
/// 값은 에셋에 숫자로 저장되므로 순서를 바꾸면 안 된다.
/// </summary>
public enum RelicRarity
{
    Common = 0,   // 1단계 — 능력치를 한 칸 올려 주는 정도
    Uncommon = 1, // 2단계 — 판을 굴리는 방식을 조금 바꾼다
    Rare = 2,     // 3단계 — 그 판의 성격을 정한다
}

/// <summary>
/// 유물 데이터. 이름, 설명, 아이콘, 효과 식별자, 희귀도를 담는다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic", fileName = "NewRelic")]
public class RelicData : ScriptableObject
{
    public string relicName;
    [TextArea] public string description;
    public Sprite icon;
    public RelicEffect effect;
    [Tooltip("상점 가격을 가르는 등급. 등장 확률과는 무관하다.")]
    public RelicRarity rarity = RelicRarity.Common;

    /// <summary>구애 시리즈는 서로 배타적이다. 하나를 얻으면 나머지는 등장 목록에서 빠진다.</summary>
    public bool IsChoiceItem =>
        effect == RelicEffect.ChoiceBand ||
        effect == RelicEffect.ChoiceSpecs ||
        effect == RelicEffect.ChoiceScarf;
}
