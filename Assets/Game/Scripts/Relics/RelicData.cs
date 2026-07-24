using UnityEngine;

public enum RelicEffect
{
    HappyEgg,     // 행복의알: 보스방 진입 전에 미리 진화
    SitrusBerry,  // 자뭉열매: 기절 시 방 진입 전 상태로 부활 (1회 소비)
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
}
