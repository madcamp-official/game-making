using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유물을 얻었을 때 화면 가운데 위쪽에 아이콘·이름·설명을 크게 띄운다.
///
/// 예전에는 공용 메시지 한 줄로 "유물 획득 — 이름: 설명"을 출력했는데, 한 줄에 다 넣으려니
/// 글자가 작고 긴 설명은 화면 밖으로 밀려났다. 전용 패널로 분리해 이름과 설명의 크기를 따로 준다.
/// </summary>
public class RelicPopup : MonoBehaviour
{
    private const int PanelWidth = 620;
    private const int BottomOffset = 48;   // 화면 아래 조작 안내 줄을 비워 둔다
    private const int Margin = 12;

    /// <summary>글자·아이콘 크기 한 벌. 폰트 크기는 모두 12의 배수여야 한다.</summary>
    private struct Tier
    {
        public int header, name, desc, icon, padding;
        public Tier(int header, int name, int desc, int icon, int padding)
        {
            this.header = header; this.name = name; this.desc = desc;
            this.icon = icon; this.padding = padding;
        }
    }

    // 큰 쪽을 먼저 쓰고, 창이 낮아 패널이 잘릴 때만 작은 쪽으로 내린다.
    private static readonly Tier[] Tiers =
    {
        new Tier(24, 48, 24, 48, 12),
        new Tier(12, 36, 24, 40, 10),
    };

    private RectTransform panel;
    private Image icon;
    private Text headerText;
    private Text nameText;
    private Text descText;
    private Coroutine routine;

    private void Awake()
    {
        Build();
        panel.gameObject.SetActive(false);
    }

    private void Build()
    {
        panel = PixelUi.MakePanel(transform, "Panel");
        // 화면 아래 가운데. 설명을 읽는 동안에도 화면 중앙의 캐릭터가 가려지지 않아야 한다.
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, BottomOffset);
        panel.sizeDelta = new Vector2(PanelWidth, 300f);

        Transform fill = panel.GetChild(0);

        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(fill, false);
        icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        icon.rectTransform.pivot = new Vector2(0.5f, 1f);

        headerText = PixelUi.MakeText(fill, "Header", Tiers[0].header, new Color(0.7f, 0.75f, 0.85f), TextAnchor.UpperCenter);
        headerText.text = "유물 획득!";
        nameText = PixelUi.MakeText(fill, "Name", Tiers[0].name, new Color(1f, 0.86f, 0.42f), TextAnchor.UpperCenter);
        descText = PixelUi.MakeText(fill, "Desc", Tiers[0].desc, Color.white, TextAnchor.UpperCenter);
    }

    /// <summary>유물 하나를 <paramref name="duration"/>초 동안 보여준다.</summary>
    public void Show(RelicData relic, float duration)
    {
        if (relic == null) return;

        icon.sprite = relic.icon;
        icon.enabled = relic.icon != null;
        nameText.text = relic.relicName;
        descText.text = relic.description;

        // 창이 좁으면 패널도 같이 줄여야 한다. 글자 높이는 폭이 정해진 뒤에 재야 정확하다.
        Rect area = ((RectTransform)transform).rect;
        float width = Mathf.Min(PanelWidth, Mathf.Max(240f, area.width - Margin * 2f));
        float available = area.height - Margin * 2f;

        float height = 0f;
        for (int i = 0; i < Tiers.Length; i++)
        {
            height = Layout(Tiers[i], width);
            if (height <= available) break;
        }

        // 위가 잘리지 않는 선에서 최대한 아래쪽에 붙인다.
        float bottom = Mathf.Min(BottomOffset, Mathf.Max(Margin, area.height - height - Margin));
        panel.anchoredPosition = new Vector2(0f, bottom);

        panel.gameObject.SetActive(true);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(HideAfter(duration));
    }

    /// <summary>주어진 크기 한 벌로 패널 안을 위에서 아래로 쌓고, 필요한 전체 높이를 돌려준다.</summary>
    private float Layout(Tier tier, float width)
    {
        headerText.fontSize = PixelUi.SnapFontSize(tier.header);
        nameText.fontSize = PixelUi.SnapFontSize(tier.name);
        descText.fontSize = PixelUi.SnapFontSize(tier.desc);
        icon.rectTransform.sizeDelta = new Vector2(tier.icon, tier.icon);
        panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);

        // 줄과 줄 사이에는 반드시 눈에 보이는 간격을 둔다. 붙여 놓으면 글자가 서로 닿는다.
        float gap = tier.padding * 0.5f;

        float y = -tier.padding;
        if (icon.enabled)
        {
            icon.rectTransform.anchoredPosition = new Vector2(0f, y);
            y -= tier.icon + gap;
        }
        y = PixelUi.StackFromTop(headerText, y, tier.padding) - gap;
        y = PixelUi.StackFromTop(nameText, y, tier.padding) - gap;
        y = PixelUi.StackFromTop(descText, y, tier.padding);

        float height = -y + tier.padding;
        panel.sizeDelta = new Vector2(width, height);
        return height;
    }

    private IEnumerator HideAfter(float duration)
    {
        // 행복의알은 획득 직후 진화 컷씬이 Time.timeScale을 0으로 만든다.
        // 스케일 시간으로 기다리면 팝업이 컷씬 내내 남으므로 실제 시간으로 센다.
        yield return new WaitForSecondsRealtime(duration);
        panel.gameObject.SetActive(false);
        routine = null;
    }
}
