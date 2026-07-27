using UnityEngine;

/// <summary>
/// 유물 효과 식별자. 값은 에셋에 숫자로 저장되므로 중간에 끼워 넣거나 순서를 바꾸면 안 된다.
/// 새 효과는 항상 맨 뒤에 추가한다.
/// </summary>
public enum RelicEffect
{
    HappyEgg = 0,     // 행복의알: 보스방 진입 전에 미리 진화
    EnergyRoot = 1,   // 기력의 덩어리: 기절 시 방 진입 전 상태로 부활 (1회 소비)
    AmuletCoin = 2,   // 부적금화: 골드 획득량 +25%
    ChoiceBand = 3,   // 구애머리띠: 근접 +50%, 원거리 -50%
    ChoiceSpecs = 4,  // 구애안경: 원거리 +50%, 근접 -50%
    ChoiceScarf = 5,  // 구애스카프: 이동 +50%, 근접·원거리 -20%
    BigRoot = 6,      // 큰뿌리: 회복량 +50%
    Leftovers = 7,    // 먹다남은음식: 전투방 클리어마다 체력 8 회복
    WideLens = 8,     // 광각렌즈: 투사체 크기 +15%
    ShellBell = 9,    // 조개껍질방울: 누적 40 피해마다 체력 3 회복
    LifeOrb = 10,     // 생명의구슬: 최대 체력 -30%, 공격력 +30%
    SitrusBerry = 11, // 자뭉열매: 획득 즉시 최대 체력의 33% 회복
}

/// <summary>
/// 유물 데이터. 이름, 설명, 아이콘, 효과 식별자를 담는다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic", fileName = "NewRelic")]
public class RelicData : ScriptableObject
{
    public string relicName;
    [TextArea] public string description;
    public Sprite icon;
    public RelicEffect effect;

    /// <summary>구애 시리즈는 서로 배타적이다. 하나를 얻으면 나머지는 등장 목록에서 빠진다.</summary>
    public bool IsChoiceItem =>
        effect == RelicEffect.ChoiceBand ||
        effect == RelicEffect.ChoiceSpecs ||
        effect == RelicEffect.ChoiceScarf;
}
