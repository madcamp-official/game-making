using UnityEngine;

/// <summary>
/// 상점 상품. E로 구매하면 골드를 소모하고 효과(체력 회복)를 적용한다.
/// </summary>
public class ShopItem : MonoBehaviour, IInteractable
{
    [SerializeField] private string itemName = "자뭉열매";
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
                // 값과 설명을 <b>줄로 나눈다.</b> 예전에는 " — "로 이어 한 줄로 만들었는데,
                // 구애 시리즈처럼 설명이 긴 유물은 그 한 줄이 화면 폭을 넘어가 양끝이
                // 잘리고 좌우 HUD(체력바·기술 칸) 뒤로 숨었다. 안내창은 줄바꿈을 하지만
                // 두 토막이 한 줄에 붙어 있으면 어디서 끊길지가 글자 수에 좌우된다 —
                // "무엇을 얼마에 사는가"와 "그게 무슨 효과인가"는 처음부터 다른 줄이어야 한다.
                cachedPrompt = relicData != null
                    ? "E : " + relicData.relicName + " 구매 (" + price + "G)\n"
                      + Flatten(relicData.description)
                    : "E : " + itemName + " 구매 (" + price + "G, 최대 체력의 +"
                      + Mathf.RoundToInt(healFraction * 100f) + "% 회복)";
            }
            return cachedPrompt;
        }
    }

    /// <summary>
    /// 유물 설명을 한 문단으로 편다. 줄바꿈과 그 뒤의 들여쓰기를 공백 하나로 바꾼다.
    ///
    /// 설명은 에셋(YAML)에 여러 줄로 적혀 있고, 이어지는 줄마다 들여쓰기가 딸려 온다.
    /// 그대로 두면 "…피해가 80%가\n    된다"처럼 문장 한가운데에 빈칸이 뭉텅이로 생긴다.
    /// 어디서 줄을 바꿀지는 <b>안내창의 폭</b>이 정해야지 에셋의 줄 모양이 정할 일이 아니다.
    /// </summary>
    private static string Flatten(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var flat = new System.Text.StringBuilder(text.Length);
        bool blank = false;
        foreach (char c in text)
        {
            if (c == '\n' || c == '\r' || c == ' ' || c == '\t')
            {
                blank = true;
                continue;
            }
            if (blank && flat.Length > 0) flat.Append(' ');
            blank = false;
            flat.Append(c);
        }
        return flat.ToString();
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

        // 상인에게 알린다. 방을 뒤져 찾는 것은 사는 순간 한 번뿐이라 값이 싸고,
        // 상품마다 상인을 미리 물려 두지 않아도 된다.
        ShopKeeper keeper = GetComponentInParent<ShopKeeper>();
        if (keeper == null && transform.parent != null)
            keeper = transform.parent.GetComponentInChildren<ShopKeeper>(true);
        if (keeper != null) keeper.OnPurchased();

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

            // 회복약도 돈 주고 산 물건이다. 유물 쪽은 AddRelic이 알아서 울리므로 여기서만 낸다 —
            // 양쪽에 다 넣으면 유물을 살 때 소리가 두 번 겹친다.
            GameAudio.PlayItemAcquired();
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage(itemName + "을(를) 먹고 체력을 " + heal + " 회복했다!", 2f);
        }

        // 판매된 상품은 화면에서 제거
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
    }
}
