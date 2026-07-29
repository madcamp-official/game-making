using UnityEngine;

/// <summary>
/// 상점 상품. E로 구매하면 골드를 소모하고 효과(체력 회복)를 적용한다.
/// </summary>
public class ShopItem : MonoBehaviour, IInteractable
{
    [SerializeField] private string itemName = "포션";
    [SerializeField, Min(0)] private int price = 10;
    [Tooltip("회복량은 최대 체력에 비례한다. 고정 수치가 아니라 비율인 이유는, 층마다 " +
             "최대 체력이 유물로 달라져도 포션 한 병의 값어치가 같아야 하기 때문이다.")]
    [SerializeField, Range(0f, 1f)] private float healFraction = 0.33f;
    [SerializeField] private RelicData relicData; // 지정하면 회복 대신 유물을 판매

    private bool sold;
    private string cachedPrompt; // 매 프레임 문자열 생성 방지

    public bool CanInteract => !sold;

    /// <summary>회복 상품으로 설정한다 (ShopController가 호출).</summary>
    public void ConfigureHeal(string displayName, float fraction, int cost, Sprite icon)
    {
        itemName = displayName;
        healFraction = fraction;
        price = cost;
        relicData = null;
        cachedPrompt = null;
        if (icon != null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = icon;
        }
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
                // 설명이 여러 줄인 유물(구애 시리즈)이 있다. 상호작용 안내는 한 줄짜리라
                // 줄바꿈을 그대로 넣으면 화면 아래가 두 줄로 벌어진다.
                cachedPrompt = relicData != null
                    ? "E : " + relicData.relicName + " 구매 (" + price + "G) — "
                      + relicData.description.Replace("\n", " · ")
                    : "E : " + itemName + " 구매 (" + price + "G, 최대 체력의 +"
                      + Mathf.RoundToInt(healFraction * 100f) + "% 회복)";
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
            // 회복량은 살 때 계산한다. 진열한 뒤에 최대 체력이 바뀌어도(맥스업·생명의구슬)
            // 지금 몸 기준으로 채워야 표시한 비율과 어긋나지 않는다.
            int heal = health != null ? GameMath.RoundHalfUp(health.MaxHealth * healFraction) : 0;
            if (health != null) health.Heal(heal);
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage(itemName + "을(를) 마셔 체력을 " + heal + " 회복했다!", 2f);
        }

        // 판매된 상품은 화면에서 제거
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
    }
}
