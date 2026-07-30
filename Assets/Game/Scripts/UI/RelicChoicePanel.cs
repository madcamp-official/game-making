using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 유물 두 개 중 하나를 고르는 창 (다우징머신). 보스방 보상이 "받는 것"에서 "고르는 것"으로 바뀐다.
///
/// <see cref="MoveUpgradePanel"/>과 같은 방식이다 — 씬에 EventSystem이 없어 uGUI 버튼을 쓸 수
/// 없으므로 마우스 좌표를 카드 사각형에 직접 대 보고, 숫자키로도 고를 수 있게 뒀다.
/// 고르는 동안에는 <see cref="Time.timeScale"/>을 0으로 세우므로 시간은 전부 언스케일드로 읽는다.
///
/// 강화 팔레트와 달리 <b>닫을 방법이 없다</b>. 고르지 않고 넘어가면 보상이 사라지기 때문이다.
/// </summary>
public class RelicChoicePanel : MonoBehaviour
{
    /// <summary>창이 떠 있는 동안 공격·상호작용 입력을 막기 위한 표시.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>정적 값이라 플레이 모드를 다시 시작해도 살아남는다. 판마다 초기화한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpenFlag() => IsOpen = false;

    private const int PanelWidth = 660;
    /// <summary>글자가 짧아도 이만큼은 잡는다. 아이콘(64)이 들어갈 자리이기도 하다.</summary>
    private const int MinCardHeight = 108;
    private const int CardGap = 10;
    private const int Padding = 16;
    private const int IconSize = 64;
    private const int OptionCount = 2;
    /// <summary>제목이 차지하는 세로 폭.</summary>
    private const int HeaderBlock = 56;
    /// <summary>아래 조작 안내가 차지하는 세로 폭.</summary>
    /// <summary>카드 안쪽 위아래 여백. 글자가 테두리에 닿지 않게 둔다.</summary>
    private const int CardTextPadding = 12;

    // 카드 폭은 패널에서 좌우 여백을 뺀 값으로 고정돼 있고, 글자는 그 안에서 아이콘 자리를
    // 또 비켜 앉는다. 카드 높이를 글자에 맞춰 정하려면 줄바꿈 폭을 배치 전에 알아야 해서
    // 런타임 rect가 아니라 여기서 미리 계산해 둔다.
    private const float CardWidth = PanelWidth - Padding * 2;
    private const float CardTextWidth = CardWidth - (Padding * 2 + IconSize) - Padding;

    /// <summary>고를 수 있는 칸은 전부 같은 붉은 버튼이다 (<see cref="PmdUi.MakeButton"/>).</summary>
    private static void Highlight(Image card, Text label, bool hovered)
    {
        card.sprite = hovered ? PmdUi.ButtonOnSprite : PmdUi.ButtonSprite;
        label.color = hovered ? PmdUi.HighlightColor : PmdUi.TextColor;
    }

    private RectTransform panel;
    private Image dim;
    private readonly RectTransform[] cards = new RectTransform[OptionCount];
    private readonly Image[] cardImages = new Image[OptionCount];
    private readonly Image[] cardIcons = new Image[OptionCount];
    private readonly Text[] cardTexts = new Text[OptionCount];
    private readonly RelicData[] shown = new RelicData[OptionCount];

    private Action<RelicData> onChosen;
    private float savedTimeScale = 1f;
    private int openedFrame = -1;
    /// <summary>지난 프레임에 가리키던 칸. 칸이 바뀐 순간에만 소리를 내려고 들고 있는다.</summary>
    private int lastHovered = -1;

    private void Awake()
    {
        Build();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        // timeScale은 씬이 바뀌어도 유지되는 전역값이라, 창이 뜬 채로 씬이 내려가면
        // 다음 씬이 멈춘 채 시작한다. 반드시 시간을 되살리며 닫는다.
        if (IsOpen) Close();
    }

    private void Build()
    {
        GameObject dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(transform, false);
        dim = dimGo.AddComponent<Image>();
        dim.sprite = PrimitiveSprites.Square;
        dim.color = new Color(0f, 0f, 0f, 0.45f);
        dim.raycastTarget = false;
        RectTransform dimRt = dim.rectTransform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        panel = PixelUi.MakePanel(transform, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelWidth, MinCardHeight);   // 실제 높이는 Layout이 정한다

        Image panelFill = panel.GetChild(0).GetComponent<Image>();
        // 대화창과 같은 남색을 쓰되 뒤가 살짝 비치게 둔다.
        panelFill.color = new Color(PmdUi.PanelFill.r, PmdUi.PanelFill.g, PmdUi.PanelFill.b, 0.86f);

        Text header = PixelUi.MakeText(panel, "Header", 36, new Color(1f, 0.86f, 0.42f),
                                       TextAnchor.UpperCenter);
        header.text = "유물 선택";
        RectTransform headerRt = header.rectTransform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(-Padding * 2, 44f);
        headerRt.anchoredPosition = new Vector2(0f, -Padding);

        for (int i = 0; i < OptionCount; i++)
        {
            PmdUi.Entry entry = PmdUi.MakeButton(panel, "Card" + i, "", 24);
            Image image = entry.panel;

            RectTransform rt = entry.rect;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-Padding * 2, MinCardHeight);

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(rt, false);
            Image icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform iconRt = icon.rectTransform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.sizeDelta = new Vector2(IconSize, IconSize);
            iconRt.anchoredPosition = new Vector2(Padding, 0f);

            // 버튼이 만들어 준 글자를 아이콘 오른쪽으로 밀고 왼쪽 정렬로 바꾼다.
            Text text = entry.label;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform textRt = text.rectTransform;
            textRt.offsetMin = new Vector2(Padding * 2 + IconSize, CardTextPadding);
            textRt.offsetMax = new Vector2(-Padding, -CardTextPadding);

            cards[i] = rt;
            cardImages[i] = image;
            cardIcons[i] = icon;
            cardTexts[i] = text;
        }

        // 조작 안내("클릭 또는 1 · 2 키로 선택")는 두지 않는다. 창이 뜨면 눌러 보는 것이
        // 사람이 먼저 하는 일이고, 안내 한 줄이 늘 붙어 있으면 정작 읽어야 할 유물 설명의
        // 무게가 깎인다.

        Layout();
    }

    /// <summary>
    /// 카드 높이를 글자 수에 맞추고, 패널을 그 합에 맞춘다.
    ///
    /// 고정 높이로 두면 설명이 두 줄인 유물(구애 시리즈)에서 글자가 카드 밖으로 흘러나온다.
    /// 카드는 세로만 늘어난다 — 가로는 패널 폭에 묶여 있어야 줄바꿈 폭이 변하지 않는다.
    /// </summary>
    private void Layout()
    {
        float top = Padding + HeaderBlock;

        for (int i = 0; i < OptionCount; i++)
        {
            float textHeight = PixelUi.LineBoxHeight(cardTexts[i], CardTextWidth);
            float height = Mathf.Max(MinCardHeight, textHeight + CardTextPadding * 2f);

            cards[i].sizeDelta = new Vector2(-Padding * 2, height);
            cards[i].anchoredPosition = new Vector2(0f, -top);
            top += height + CardGap;
        }

        // 마지막 카드 뒤에 붙은 칸 사이 간격은 아래 여백이 아니므로 되돌린다.
        panel.sizeDelta = new Vector2(PanelWidth, top - CardGap + Padding);
    }

    /// <summary>두 유물을 보여 주고 하나를 고르게 한다. 이미 열려 있거나 값이 비면 false.</summary>
    public bool Open(RelicData first, RelicData second, Action<RelicData> chosen)
    {
        if (IsOpen || first == null || second == null || chosen == null) return false;

        shown[0] = first;
        shown[1] = second;
        onChosen = chosen;

        for (int i = 0; i < OptionCount; i++)
        {
            cardIcons[i].sprite = shown[i].icon;
            cardIcons[i].enabled = shown[i].icon != null;
            cardTexts[i].text = (i + 1) + ".  " + shown[i].relicName + "\n" + shown[i].description;
            Highlight(cardImages[i], cardTexts[i], false);
        }

        // 글자를 넣은 뒤에 다시 잰다. 유물마다 설명 길이가 달라 카드 높이도 매번 달라진다.
        Layout();

        SetVisible(true);
        IsOpen = true;
        // 마지막 적을 잡은 클릭이 그대로 카드를 눌러 버리지 않도록 열린 프레임은 입력을 받지 않는다.
        openedFrame = Time.frameCount;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        return true;
    }

    private void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;

        Mouse mouse = Mouse.current;
        int hovered = -1;
        if (mouse != null)
        {
            Vector2 screenPos = mouse.position.ReadValue();
            for (int i = 0; i < OptionCount; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(cards[i], screenPos, null)) continue;
                hovered = i;
                break;
            }
        }

        // 칸이 바뀐 순간에만 커서음을 낸다. 판단은 PmdUi가 맡는다 —
        // 창마다 hover 판정을 따로 돌리는 구조라(이 씬에는 EventSystem이 없다)
        // 그 규칙까지 창마다 흩어지면 창을 하나 더 만들 때마다 다시 정하게 된다.
        lastHovered = PmdUi.TrackHoverSound(lastHovered, hovered);

        for (int i = 0; i < OptionCount; i++)
            Highlight(cardImages[i], cardTexts[i], i == hovered);

        if (hovered >= 0 && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Choose(hovered);
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) Choose(0);
        else if (kb.digit2Key.wasPressedThisFrame) Choose(1);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= OptionCount) return;
        RelicData picked = shown[index];
        Action<RelicData> callback = onChosen;

        // 먼저 닫는다. 콜백이 유물을 지급하면 획득 팝업이 뜨는데, 그때 이 창이 아직 떠 있으면
        // 팝업이 가려진다. 시간도 여기서 되살아나야 진화 컷씬 같은 다음 연출이 이어서 돈다.
        Close();

        if (callback != null && picked != null) callback(picked);
    }

    private void Close()
    {
        IsOpen = false;
        onChosen = null;
        SetVisible(false);
        Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
    }

    private void SetVisible(bool visible)
    {
        if (dim != null) dim.gameObject.SetActive(visible);
        if (panel != null) panel.gameObject.SetActive(visible);
    }
}
