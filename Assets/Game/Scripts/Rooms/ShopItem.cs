using UnityEngine;

/// <summary>
/// 상점 상품. E로 구매하면 골드를 소모하고 효과(체력 회복)를 적용한다.
/// </summary>
public class ShopItem : MonoBehaviour, IInteractable
{
    [SerializeField] private string itemName = "포션";
    [SerializeField, Min(0)] private int price = 10;
    [SerializeField, Min(0)] private int healAmount = 5;
    [SerializeField] private RelicData relicData; // 지정하면 회복 대신 유물을 판매

    private bool sold;
    private string cachedPrompt; // 매 프레임 문자열 생성 방지

    public bool CanInteract => !sold;

    /// <summary>회복 상품으로 설정한다 (ShopController가 호출).</summary>
    public void ConfigureHeal(string displayName, int heal, int cost)
    {
        itemName = displayName;
        healAmount = heal;
        price = cost;
        relicData = null;
        cachedPrompt = null;
    }

    /// <summary>유물 상품으로 설정하고 아이콘을 표시한다 (ShopController가 호출).</summary>
    public void ConfigureRelic(RelicData relic, int cost)
    {
        relicData = relic;
        price = cost;
        cachedPrompt = null;
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && relic != null && relic.icon != null) sr.sprite = relic.icon;
    }

    public string Prompt
    {
        get
        {
            if (cachedPrompt == null)
            {
                cachedPrompt = relicData != null
                    ? "E : " + relicData.relicName + " 구매 (" + price + "G) — " + relicData.description
                    : "E : " + itemName + " 구매 (" + price + "G, 체력 +" + healAmount + ")";
            }
            return cachedPrompt;
        }
    }

    public void Interact(GameObject interactor)
    {
        if (sold) return;

        if (RunManager.Instance == null || !RunManager.Instance.SpendGold(price))
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("골드가 부족하다...", 1.5f);
            return;
        }

        sold = true;

        if (relicData != null)
        {
            if (RelicManager.Instance != null) RelicManager.Instance.AddRelic(relicData);
        }
        else
        {
            Health health = interactor.GetComponent<Health>();
            if (health != null) health.Heal(healAmount);
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage(itemName + "을(를) 마셔 체력을 " + healAmount + " 회복했다!", 2f);
        }

        // 판매된 상품은 화면에서 제거
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
    }
}
