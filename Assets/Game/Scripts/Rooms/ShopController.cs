using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 구성: 상품 4개 — 포션 1개 + 유물 3개.
///
/// 유물은 <see cref="RelicManager"/>의 등장 순서에서 앞에서부터 꺼낸다. 꺼낸 유물은 사지 않고
/// 지나가더라도 더미가 마를 때까지는 다시 등장하지 않고, 더미가 마르면 아직 손에 넣지 않은
/// 유물로 다시 채워진다. 그래서 <b>얻을 수 있는 유물이 남아 있는 한 이 칸은 비지 않는다</b>.
/// 정말 다 모았을 때만 남는 칸을 끈다.
/// </summary>
public class ShopController : MonoBehaviour
{
    [SerializeField] private ShopItem healSlot;
    [SerializeField] private ShopItem[] relicSlots; // 3개

    [Header("포션 — 자뭉열매")]
    [Tooltip("층별 가격. 1·2·3층 순서다. 층이 더 늘면 마지막 값을 쓴다.")]
    [SerializeField] private int[] potionPrices = { 10, 20, 30 };
    [Tooltip("최대 체력의 몇 할을 회복할지. 유물에서 빠진 자뭉열매의 효과를 그대로 물려받았다.")]
    [SerializeField, Range(0f, 1f)] private float potionHealFraction = 0.33f;
    [Tooltip("진열대에 놓는 이름. 그림도 이름도 자뭉열매라 안내문만 '포션'이면 딴것으로 읽힌다.")]
    [SerializeField] private string potionName = "자뭉열매";
    [Tooltip("포션 그림. 자뭉열매 아이콘을 쓴다.")]
    [SerializeField] private Sprite potionIcon;
    // 유물 가격은 여기 없다. 희귀도마다 다르고, 그 표는 RelicManager가 들고 있다.

    /// <summary>
    /// 지금 층의 포션 가격. 회복량이 최대 체력 비례라 층이 올라가도 값이 줄지 않는데,
    /// 골드 수입은 층마다 늘어난다. 가격도 함께 올리지 않으면 후반에는 공짜나 다름없어진다.
    /// </summary>
    private int PotionPrice
    {
        get
        {
            if (potionPrices == null || potionPrices.Length == 0) return 0;
            int floor = RoomFlowController.Instance != null
                ? RoomFlowController.Instance.CurrentFloorIndex : 0;
            return Mathf.Max(0, potionPrices[Mathf.Clamp(floor, 0, potionPrices.Length - 1)]);
        }
    }

    private void Start()
    {
        if (healSlot != null)
            healSlot.ConfigureHeal(potionName, potionHealFraction, PotionPrice, potionIcon);

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
