using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 레벨이 오를 때 뜨는 기술 강화 선택지 팔레트. 세 개 중 하나를 고른다.
///
/// 씬에 EventSystem이 없어서 uGUI 버튼을 쓸 수 없다. <see cref="RelicTooltip"/>과 같은 방식으로
/// 마우스 좌표를 직접 카드 사각형에 대 보고, 숫자키 1·2·3으로도 고를 수 있게 뒀다.
///
/// 고르는 동안에는 <see cref="Time.timeScale"/>을 0으로 세운다. 그래서 여기서는 시간을 전부
/// 언스케일드로 읽어야 한다.
/// </summary>
public class MoveUpgradePanel : MonoBehaviour
{
    /// <summary>팔레트가 떠 있는 동안 공격·상호작용 입력을 막기 위한 표시.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// 정적 값이라 플레이 모드를 다시 시작해도 살아남는다. 팔레트가 열린 채로 판이 끝나면
    /// 다음 판이 "열려 있는" 상태로 시작해 공격 입력이 통째로 막힌다. 판마다 초기화한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpenFlag() => IsOpen = false;

    private const int PanelWidth = 660;
    private const int CardHeight = 76;
    private const int CardGap = 10;
    private const int Padding = 16;
    private const int OptionCount = 3;

    private static readonly Color CardColor = new Color(0.16f, 0.2f, 0.3f, 0.72f);
    private static readonly Color CardHoverColor = new Color(0.28f, 0.42f, 0.62f, 0.85f);

    private RectTransform panel;
    private Image dim;
    private readonly List<RectTransform> cards = new List<RectTransform>();
    private readonly List<Image> cardImages = new List<Image>();
    private readonly List<Text> cardTexts = new List<Text>();
    private readonly List<MoveUpgradeOption> shown = new List<MoveUpgradeOption>();

    private PlayerMoves moves;
    private float savedTimeScale = 1f;
    private int openedFrame = -1;

    private void Awake()
    {
        Build();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        // timeScale은 씬이 바뀌어도 유지되는 전역값이라, 팔레트가 뜬 채로 씬이 내려가면
        // 다음 씬이 멈춘 채 시작한다. 반드시 시간을 되살리며 닫는다.
        if (IsOpen) Close(true);
    }

    private void Build()
    {
        // 뒤쪽 화면을 살짝 눌러 둔다. 팔레트 자체가 반투명이라 이게 없으면 글자가 안 읽힌다.
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

        int height = Padding * 2 + 56 + OptionCount * (CardHeight + CardGap) + 28;
        panel = PixelUi.MakePanel(transform, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelWidth, height);

        Image panelFill = panel.GetChild(0).GetComponent<Image>();
        // "반투명 팔레트" — 뒤쪽 전투 상황이 비쳐 보여야 한다.
        panelFill.color = new Color(0.05f, 0.06f, 0.1f, 0.72f);

        Text header = PixelUi.MakeText(panel, "Header", 36, new Color(1f, 0.9f, 0.4f),
                                       TextAnchor.UpperCenter);
        header.text = "기술 강화";
        RectTransform headerRt = header.rectTransform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(-Padding * 2, 44f);
        headerRt.anchoredPosition = new Vector2(0f, -Padding);

        for (int i = 0; i < OptionCount; i++)
        {
            GameObject cardGo = new GameObject("Card" + i);
            cardGo.transform.SetParent(panel, false);
            Image image = cardGo.AddComponent<Image>();
            image.sprite = PrimitiveSprites.Square;
            image.color = CardColor;
            image.raycastTarget = false;

            RectTransform rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-Padding * 2, CardHeight);
            rt.anchoredPosition = new Vector2(0f, -(Padding + 56 + i * (CardHeight + CardGap)));

            Text text = PixelUi.MakeText(rt, "Text", 24, Color.white, TextAnchor.MiddleCenter);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            cards.Add(rt);
            cardImages.Add(image);
            cardTexts.Add(text);
        }

        Text hint = PixelUi.MakeText(panel, "Hint", 12, new Color(0.8f, 0.8f, 0.85f, 0.8f),
                                     TextAnchor.LowerCenter);
        hint.text = "클릭 또는 1 · 2 · 3 키로 선택";
        RectTransform hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(-Padding * 2, 24f);
        hintRt.anchoredPosition = new Vector2(0f, 8f);
    }

    /// <summary>선택지를 뽑아 팔레트를 연다. 남은 강화가 하나도 없으면 열지 않고 false.</summary>
    public bool Open(PlayerMoves playerMoves)
    {
        if (IsOpen || playerMoves == null) return false;

        List<MoveUpgradeOption> rolled = playerMoves.RollUpgrades(OptionCount);
        if (rolled.Count == 0) return false;

        moves = playerMoves;
        shown.Clear();
        shown.AddRange(rolled);

        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < shown.Count;
            cards[i].gameObject.SetActive(used);
            if (!used) continue;
            cardTexts[i].text = (i + 1) + ".  " + shown[i].title + " — " + shown[i].detail;
            cardImages[i].color = CardColor;
        }

        SetVisible(true);
        IsOpen = true;
        openedFrame = Time.frameCount;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        return true;
    }

    private void Update()
    {
        if (!IsOpen) return;
        // 마지막 적을 잡은 공격 클릭과 같은 프레임에 팔레트가 열린다. 그 클릭이 카드까지
        // 눌러 버리지 않도록, 열린 프레임에는 입력을 받지 않는다.
        if (Time.frameCount == openedFrame) return;

        Mouse mouse = Mouse.current;
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

        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame && shown.Count > 0) Choose(0);
        else if (kb.digit2Key.wasPressedThisFrame && shown.Count > 1) Choose(1);
        else if (kb.digit3Key.wasPressedThisFrame && shown.Count > 2) Choose(2);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= shown.Count) return;
        MoveUpgradeOption option = shown[index];
        if (moves != null) moves.ApplyUpgrade(option.id);
        Close(true);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage(option.title + " 강화! " + option.detail, 2.5f);
    }

    private void Close(bool restoreTime)
    {
        IsOpen = false;
        SetVisible(false);
        if (restoreTime) Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
    }

    private void SetVisible(bool visible)
    {
        if (dim != null) dim.gameObject.SetActive(visible);
        if (panel != null) panel.gameObject.SetActive(visible);
    }
}
