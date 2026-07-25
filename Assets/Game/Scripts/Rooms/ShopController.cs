using UnityEngine;

/// <summary>
/// 상점 구성 (gameplay-spec 12절): 상품 3개 — 체력 회복 1개 + 무작위 유물 2개.
/// 유물 두 개는 상품 목록(relicPool)에서 무작위로 뽑는다.
/// </summary>
public class ShopController : MonoBehaviour
{
    [SerializeField] private ShopItem healSlot;
    [SerializeField] private ShopItem[] relicSlots; // 2개
    [SerializeField] private RelicData[] relicPool;

    [SerializeField, Min(0)] private int potionPrice = 10;
    [SerializeField, Min(0)] private int potionHeal = 30;
    [SerializeField, Min(0)] private int relicPrice = 15;

    private void Start()
    {
        if (healSlot != null)
            healSlot.ConfigureHeal("포션", potionHeal, potionPrice);

        if (relicSlots == null || relicPool == null || relicPool.Length == 0) return;

        // 풀에서 무작위 비복원 추출 (풀이 슬롯보다 적으면 중복 허용)
        int poolCount = relicPool.Length;
        int first = Random.Range(0, poolCount);
        for (int i = 0; i < relicSlots.Length; i++)
        {
            if (relicSlots[i] == null) continue;
            int pick;
            if (i == 0 || poolCount < 2)
                pick = i == 0 ? first : Random.Range(0, poolCount);
            else
            {
                pick = Random.Range(0, poolCount - 1);
                if (pick >= first) pick++; // 첫 번째와 다른 유물 선택
            }
            relicSlots[i].ConfigureRelic(relicPool[pick], relicPrice);
        }
    }
}
