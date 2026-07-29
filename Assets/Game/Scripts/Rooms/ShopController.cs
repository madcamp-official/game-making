using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 구성 (gameplay-spec 12절): 상품 3개 — 체력 회복 1개 + 유물 2개.
///
/// 유물은 <see cref="RelicManager"/>의 등장 순서에서 앞에서부터 꺼낸다. 꺼낸 유물은 사지 않고
/// 지나가더라도 더미가 마를 때까지는 다시 등장하지 않고, 더미가 마르면 아직 손에 넣지 않은
/// 유물로 다시 채워진다. 그래서 <b>얻을 수 있는 유물이 남아 있는 한 이 칸은 비지 않는다</b>.
/// 정말 다 모았을 때만 남는 칸을 끈다.
/// </summary>
public class ShopController : MonoBehaviour
{
    [SerializeField] private ShopItem healSlot;
    [SerializeField] private ShopItem[] relicSlots; // 2개

    [SerializeField, Min(0)] private int potionPrice = 10;
    [SerializeField, Min(0)] private int potionHeal = 30;
    // 유물 가격은 여기 없다. 희귀도마다 다르고, 그 표는 RelicManager가 들고 있다.

    private void Start()
    {
        if (healSlot != null)
            healSlot.ConfigureHeal("포션", potionHeal, potionPrice);

        if (relicSlots == null || relicSlots.Length == 0) return;

        RelicManager relics = RelicManager.Instance;
        List<RelicData> drawn = relics != null
            ? relics.DrawNext(relicSlots.Length)
            : new List<RelicData>();

        for (int i = 0; i < relicSlots.Length; i++)
        {
            if (relicSlots[i] == null) continue;
            // 가격은 진열할 때 한 번만 뽑는다. PriceOf는 부를 때마다 값이 흔들린다.
            if (i < drawn.Count) relicSlots[i].ConfigureRelic(drawn[i], relics.PriceOf(drawn[i]));
            else relicSlots[i].gameObject.SetActive(false);   // 더 나올 유물이 없다
        }
    }
}
