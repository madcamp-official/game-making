using UnityEngine;

/// <summary>
/// 상점 상품. E로 구매하면 골드를 소모하고 효과(체력 회복)를 적용한다.
/// </summary>
public class ShopItem : MonoBehaviour, IInteractable
{
    [SerializeField] private string itemName = "포션";
    [SerializeField] private int price = 10;
    [SerializeField] private int healAmount = 5;

    private bool sold;

    public bool CanInteract => !sold;
    public string Prompt => "E : " + itemName + " 구매 (" + price + "G, 체력 +" + healAmount + ")";

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
        Health health = interactor.GetComponent<Health>();
        if (health != null) health.Heal(healAmount);
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage(itemName + "을(를) 마셔 체력을 " + healAmount + " 회복했다!", 2f);

        // 판매된 상품은 흐리게 표시
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.25f);
    }
}
