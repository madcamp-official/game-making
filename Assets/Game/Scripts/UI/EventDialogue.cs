using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 이벤트 대사창과 선택지 팝업.
///
/// 대사창은 화면 위쪽에 붙되 골드(왼쪽 위)와 방 이름(가운데 위)을 가리지 않도록 그 아래에서
/// 시작한다. 선택지는 그 밑에 기술 강화 팔레트와 같은 카드 형태로 놓고, 해로운 효과는 붉은색,
/// 이로운 효과는 초록색으로 쓴다.
///
/// 씬에 EventSystem이 없어서 uGUI 버튼을 못 쓴다. <see cref="MoveUpgradePanel"/>과 같은 방식으로
/// 마우스 좌표를 카드 사각형에 직접 대 보고, 숫자키 1·2·3도 받는다.
/// </summary>
public class EventDialogue : MonoBehaviour
{
    /// <summary>대사창이 떠 있는 동안 공격·상호작용 입력을 막기 위한 표시.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>정적 값이라 판이 바뀌어도 살아남는다. 켜진 채로 끝나면 다음 판이 먹통이 된다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpenFlag() => IsOpen = false;

    // HUD의 골드·방 이름이 화면 위 60px 높이(y -20 ~ -80)를 쓴다. 그 아래에서 시작한다.
    private const float TopMargin = 96f;
    // 아래쪽 조작 힌트(y 40, 높이 60)를 덮지 않도록 남겨 두는 자리.
    private const float BottomMargin = 108f;
    private const float MaxWidth = 1080f;
    private const float ScreenPadding = 30f;
    private const float PanelGap = 12f;
    private const int Padding = 16;
    private const float CardGap = 8f;
    private const float CardPadding = 10f;

    /// <summary>
    /// 글자 크기 단계. 캔버스가 Constant Pixel Size라 UI가 절대 픽셀이어서, 창이 작으면
    /// 선택지가 화면 아래로 잘려 나간다. 큰 단계부터 넣어 보고 안 들어가면 한 단계 줄인다.
    /// PMD 비트맵 폰트라 크기는 12의 배수만 쓸 수 있다 (<see cref="PixelUi.SnapFontSize"/>).
    /// </summary>
    private struct Tier
    {
        public int fontSize;
        public float lineHeight;
        public float portraitSize;
        public float minDialogueHeight;
    }

    private static readonly Tier[] Tiers =
    {
        new Tier { fontSize = 24, lineHeight = 30f, portraitSize = 96f, minDialogueHeight = 132f },
        new Tier { fontSize = 12, lineHeight = 18f, portraitSize = 56f, minDialogueHeight = 80f },
    };

    private Tier tier = Tiers[0];

    private static readonly Color GoodColor = new Color(0.4f, 0.92f, 0.45f);
    private static readonly Color BadColor = new Color(1f, 0.38f, 0.36f);
    private static readonly Color CardColor = new Color(0.16f, 0.2f, 0.3f, 0.78f);
    private static readonly Color CardHoverColor = new Color(0.28f, 0.42f, 0.62f, 0.9f);

    private Image dim;
    private RectTransform dialogue;
    private Image portrait;
    private Text bodyText;
    private RectTransform choicePanel;

    private readonly List<RectTransform> cards = new List<RectTransform>();
    private readonly List<Image> cardImages = new List<Image>();
    private readonly List<EventChoice> shown = new List<EventChoice>();

    private Action onClosed;
    private float savedTimeScale = 1f;
    private int openedFrame = -1;
    /// <summary>결과를 보여 주는 중. 이때는 아무 데나 눌러도 닫힌다.</summary>
    private bool awaitingDismiss;
    private EventOutcome pendingOutcome;

    private float Width => Mathf.Min(MaxWidth, Screen.width - ScreenPadding * 2f);

    private void Awake()
    {
        Build();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        // 대사창이 뜬 채로 씬이 내려가면 시간이 멈춘 채 남는다. timeScale은 전역값이다.
        if (IsOpen) Close();
    }

    private void Build()
    {
        GameObject dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(transform, false);
        dim = dimGo.AddComponent<Image>();
        dim.sprite = PrimitiveSprites.Square;
        // 방 상황이 비쳐 보여야 하므로 옅게만 덮는다.
        dim.color = new Color(0f, 0f, 0f, 0.35f);
        dim.raycastTarget = false;
        RectTransform dimRt = dim.rectTransform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        dialogue = PixelUi.MakePanel(transform, "Dialogue");
        dialogue.anchorMin = dialogue.anchorMax = new Vector2(0.5f, 1f);
        dialogue.pivot = new Vector2(0.5f, 1f);
        dialogue.anchoredPosition = new Vector2(0f, -TopMargin);

        GameObject portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(dialogue, false);
        portrait = portraitGo.AddComponent<Image>();
        portrait.raycastTarget = false;
        portrait.preserveAspect = true;
        RectTransform portraitRt = portrait.rectTransform;
        portraitRt.anchorMin = portraitRt.anchorMax = new Vector2(0f, 0.5f);
        portraitRt.pivot = new Vector2(0f, 0.5f);
        portraitRt.anchoredPosition = new Vector2(Padding + 4f, 0f);

        bodyText = PixelUi.MakeText(dialogue, "Body", 24, Color.white, TextAnchor.MiddleLeft);
        RectTransform bodyRt = bodyText.rectTransform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;

        choicePanel = PixelUi.MakePanel(transform, "Choices");
        choicePanel.anchorMin = choicePanel.anchorMax = new Vector2(0.5f, 1f);
        choicePanel.pivot = new Vector2(0.5f, 1f);
    }

    /// <summary>
    /// 이벤트를 연다. <paramref name="onClosed"/>는 팝업이 완전히 닫힐 때 한 번 불린다.
    /// 이미 떠 있으면 아무 일도 하지 않고 false.
    /// </summary>
    public bool Open(EventPrompt prompt, Action onClosed)
    {
        if (IsOpen || prompt == null || prompt.choices.Count == 0) return false;

        this.onClosed = onClosed;
        IsOpen = true;
        openedFrame = Time.frameCount;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        SetVisible(true);
        ShowPrompt(prompt);
        return true;
    }

    private void ShowPrompt(EventPrompt prompt)
    {
        awaitingDismiss = false;
        pendingOutcome = null;

        shown.Clear();
        shown.AddRange(prompt.choices);

        tier = PickTier();
        SetBody(prompt.intro, null, prompt.portrait);
        RebuildCards();
        choicePanel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 대사창과 선택지를 화면 안에 다 넣을 수 있는 가장 큰 글자 단계를 고른다.
    /// 다 안 들어가면 가장 작은 단계로 두고, 그래도 넘치면 그 창은 너무 작은 것이다.
    /// </summary>
    private Tier PickTier()
    {
        float available = Screen.height - TopMargin - BottomMargin;
        for (int i = 0; i < Tiers.Length; i++)
        {
            float needed = Tiers[i].minDialogueHeight + PanelGap + MeasureCards(Tiers[i]);
            if (needed <= available) return Tiers[i];
        }
        return Tiers[Tiers.Length - 1];
    }

    /// <summary>선택지 패널이 차지할 높이. 카드를 실제로 만들지 않고 계산만 한다.</summary>
    private float MeasureCards(Tier t)
    {
        float height = Padding * 2f;
        for (int i = 0; i < shown.Count; i++)
        {
            height += CardPadding * 2f + t.lineHeight * (1 + shown[i].lines.Length);
            if (i < shown.Count - 1) height += CardGap;
        }
        return height;
    }

    /// <summary>대사창 내용을 갈아 끼운다. 얼굴이 있으면 글자를 그만큼 밀어 준다.</summary>
    private void SetBody(string intro, string result, Sprite face)
    {
        bool hasFace = face != null;
        portrait.gameObject.SetActive(hasFace);
        portrait.sprite = face;
        portrait.rectTransform.sizeDelta = new Vector2(tier.portraitSize, tier.portraitSize);

        float left = Padding + (hasFace ? tier.portraitSize + Padding : 0f);
        RectTransform bodyRt = bodyText.rectTransform;
        bodyRt.offsetMin = new Vector2(left, Padding);
        bodyRt.offsetMax = new Vector2(-Padding, -Padding);

        bodyText.fontSize = PixelUi.SnapFontSize(tier.fontSize);

        // 대사와 결과를 한 화면에 같이 둔다. 대사는 따옴표를 붙여 구분한다.
        string text = intro;
        if (!string.IsNullOrEmpty(result))
            text = string.IsNullOrEmpty(intro) ? result : intro + "\n" + result;
        bodyText.text = text;

        // 글이 길면 창을 늘리되, 선택지를 밀어내지 않을 만큼만 늘린다.
        float available = Screen.height - TopMargin - BottomMargin;
        float forChoices = choicePanel.gameObject.activeSelf ? MeasureCards(tier) + PanelGap : 0f;
        float wanted = bodyText.preferredHeight + Padding * 2f;
        if (hasFace) wanted = Mathf.Max(wanted, tier.portraitSize + Padding);
        float height = Mathf.Clamp(wanted, tier.minDialogueHeight, Mathf.Max(tier.minDialogueHeight, available - forChoices));

        dialogue.sizeDelta = new Vector2(Width, height);
        choicePanel.anchoredPosition = new Vector2(0f, -(TopMargin + height + PanelGap));
    }

    private void RebuildCards()
    {
        for (int i = cards.Count - 1; i >= 0; i--)
            if (cards[i] != null) Destroy(cards[i].gameObject);
        cards.Clear();
        cardImages.Clear();

        float width = Width;
        float y = -Padding;

        for (int i = 0; i < shown.Count; i++)
        {
            EventChoice choice = shown[i];
            float height = CardPadding * 2f + tier.lineHeight * (1 + choice.lines.Length);

            GameObject cardGo = new GameObject("Card" + i);
            cardGo.transform.SetParent(choicePanel, false);
            Image image = cardGo.AddComponent<Image>();
            image.sprite = PrimitiveSprites.Square;
            image.color = CardColor;
            image.raycastTarget = false;

            RectTransform rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-Padding * 2f, height);
            rt.anchoredPosition = new Vector2(0f, y);

            Text label = PixelUi.MakeText(rt, "Label", tier.fontSize, Color.white, TextAnchor.UpperLeft);
            PlaceLine(label.rectTransform, 0f, tier.lineHeight);
            label.text = (i + 1) + ".  " + choice.label;

            for (int l = 0; l < choice.lines.Length; l++)
            {
                EventEffectLine line = choice.lines[l];
                Text lineText = PixelUi.MakeText(rt, "Line" + l, tier.fontSize,
                    line.harmful ? BadColor : GoodColor, TextAnchor.UpperLeft);
                PlaceLine(lineText.rectTransform, tier.lineHeight * (l + 1), tier.lineHeight);
                lineText.text = line.text;
            }

            cards.Add(rt);
            cardImages.Add(image);
            y -= height + CardGap;
        }

        float panelHeight = -y - CardGap + Padding;
        choicePanel.sizeDelta = new Vector2(width, panelHeight);
        // 세로 위치는 대사창 높이에 따라 달라진다. SetBody가 이미 잡아 두었다.
    }

    private static void PlaceLine(RectTransform rt, float top, float lineHeight)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-CardPadding * 2f, lineHeight);
        rt.anchoredPosition = new Vector2(0f, -(CardPadding + top));
    }

    private void Update()
    {
        if (!IsOpen) return;
        // 마지막 선택을 만든 클릭이 그대로 다음 상태까지 눌러 버리면 안 된다.
        if (Time.frameCount == openedFrame) return;

        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (awaitingDismiss)
        {
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool keyed = kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
            if (clicked || keyed) Dismiss();
            return;
        }

        int hovered = -1;
        if (mouse != null)
        {
            Vector2 screenPos = mouse.position.ReadValue();
            for (int i = 0; i < shown.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(cards[i], screenPos, null)) continue;
                hovered = i;
                break;
            }
        }

        for (int i = 0; i < shown.Count; i++)
            cardImages[i].color = i == hovered ? CardHoverColor : CardColor;

        if (hovered >= 0 && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Choose(hovered);
            return;
        }

        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame && shown.Count > 0) Choose(0);
        else if (kb.digit2Key.wasPressedThisFrame && shown.Count > 1) Choose(1);
        else if (kb.digit3Key.wasPressedThisFrame && shown.Count > 2) Choose(2);
        else if (kb.digit4Key.wasPressedThisFrame && shown.Count > 3) Choose(3);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= shown.Count) return;

        EventOutcome outcome = shown[index].resolve != null ? shown[index].resolve() : null;
        if (outcome == null) { Close(); return; }

        // 다시 물어보는 결과(잠만보 깨우기 실패)는 그 자체가 새 질문이라 클릭을 더 받지 않는다.
        // 문구가 "...어떻게 하시겠습니까?"로 끝나는데 선택지가 안 보이면 막힌 것처럼 보인다.
        if (outcome.reopenWith != null)
        {
            openedFrame = Time.frameCount;
            ShowPrompt(outcome.reopenWith);
            return;
        }

        // 결과 화면에서는 선택지를 감추고, 클릭 한 번을 더 기다린다.
        choicePanel.gameObject.SetActive(false);
        string quote = string.IsNullOrEmpty(outcome.quote) ? null : "\"" + outcome.quote + "\"";
        SetBody(quote, outcome.result, outcome.portrait);

        pendingOutcome = outcome;
        awaitingDismiss = true;
        openedFrame = Time.frameCount;
    }

    /// <summary>결과를 확인했다. 이벤트는 여기서 끝난다.</summary>
    private void Dismiss() => Close();

    private void Close()
    {
        IsOpen = false;
        awaitingDismiss = false;
        pendingOutcome = null;
        SetVisible(false);
        Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;

        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        if (dim != null) dim.gameObject.SetActive(visible);
        if (dialogue != null) dialogue.gameObject.SetActive(visible);
        if (choicePanel != null) choicePanel.gameObject.SetActive(visible);
    }
}
