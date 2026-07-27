using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 구성 (gameplay-spec 12절): 상품 3개 — 체력 회복 1개 + 유물 2개.
///
/// 유물은 <see cref="RelicManager"/>의 등장 순서에서 앞에서부터 꺼낸다. 꺼낸 유물은
/// 사지 않고 지나가더라도 다시 등장하지 않는다. 남은 유물이 없으면 그 칸은 비운다.
/// </summary>
public class ShopController : MonoBehaviour
{
    [SerializeField] private ShopItem healSlot;
    [SerializeField] private ShopItem[] relicSlots; // 2개

    [SerializeField, Min(0)] private int potionPrice = 10;
    [SerializeField, Min(0)] private int potionHeal = 30;
    [SerializeField, Min(0)] private int relicPrice = 15;

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
            if (i < drawn.Count) relicSlots[i].ConfigureRelic(drawn[i], relicPrice);
            else relicSlots[i].gameObject.SetActive(false);   // 더 나올 유물이 없다
        }
    }
}
