using UnityEngine;

/// <summary>
/// 한 층의 구성 데이터: 이름과 5개 방 프리팹 순서.
/// </summary>
[CreateAssetMenu(menuName = "Game/Floor", fileName = "NewFloor")]
public class FloorData : ScriptableObject
{
    public string floorName;
    public GameObject[] roomPrefabs;
    public string[] roomNames;
}
