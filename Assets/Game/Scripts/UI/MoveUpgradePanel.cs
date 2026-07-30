using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 레벨이 오를 때 뜨는 기술 강화 선택지 팔레트. 세 개 중 하나를 고른다
/// (기술머신을 지니고 있으면 네 개).
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
    private const int DefaultOptionCount = 3;
    /// <summary>기술머신까지 감안한 최대 칸 수. 카드는 이 수만큼 미리 만들어 두고 필요한 만큼만 켠다.</summary>
    private const int MaxOptionCount = 4;

    /// <summary>고를 수 있는 칸은 전부 같은 붉은 버튼이다 (<see cref="PmdUi.MakeButton"/>).</summary>
    private static void Highlight(Image card, Text label, bool hovered)
    {
        card.sprite = hovered ? PmdUi.ButtonOnSprite : PmdUi.ButtonSprite;
        label.color = hovered ? PmdUi.HighlightColor : PmdUi.TextColor;
    }

    private RectTransform panel;
    private Image dim;
    private readonly List<RectTransform> cards = new List<RectTransform>();
    private readonly List<Image> cardImages = new List<Image>();
    private readonly List<Text> cardTexts = new List<Text>();
    private readonly List<MoveUpgradeOption> shown = new List<MoveUpgradeOption>();

    private Text hintText;
    private PlayerMoves moves;
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

        int height = PanelHeight(DefaultOptionCount);
        panel = PixelUi.MakePanel(transform, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelWidth, height);

        Image panelFill = panel.GetChild(0).GetComponent<Image>();
        // "반투명 팔레트" — 뒤쪽 전투 상황이 비쳐 보여야 한다. 색은 대화창의 남색 그대로 두고
        // 투명도만 낮춘다. 다른 색을 쓰면 창 하나만 다른 게임에서 온 것처럼 보인다.
        panelFill.color = new Color(PmdUi.PanelFill.r, PmdUi.PanelFill.g, PmdUi.PanelFill.b, 0.82f);

        Text header = PixelUi.MakeText(panel, "Header", 36, new Color(1f, 0.9f, 0.4f),
                                       TextAnchor.UpperCenter);
        header.text = "기술 강화";
        RectTransform headerRt = header.rectTransform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(-Padding * 2, 44f);
        headerRt.anchoredPosition = new Vector2(0f, -Padding);

        for (int i = 0; i < MaxOptionCount; i++)
        {
            PmdUi.Entry entry = PmdUi.MakeButton(panel, "Card" + i, "", 24);
            Image image = entry.panel;

            RectTransform rt = entry.rect;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-Padding * 2, CardHeight);
            rt.anchoredPosition = new Vector2(0f, -(Padding + 56 + i * (CardHeight + CardGap)));

            Text text = entry.label;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            cards.Add(rt);
            cardImages.Add(image);
            cardTexts.Add(text);
        }

        hintText = PixelUi.MakeText(panel, "Hint", 12, new Color(0.8f, 0.8f, 0.85f, 0.8f),
                                    TextAnchor.LowerCenter);
        RectTransform hintRt = hintText.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(-Padding * 2, 24f);
        hintRt.anchoredPosition = new Vector2(0f, 8f);
    }

    private static int PanelHeight(int optionCount) =>
        Padding * 2 + 56 + optionCount * (CardHeight + CardGap) + 28;

    /// <summary>선택지를 뽑아 팔레트를 연다. 남은 강화가 하나도 없으면 열지 않고 false.</summary>
    public bool Open(PlayerMoves playerMoves)
    {
        if (IsOpen || playerMoves == null) return false;

        // 기술머신은 고를 수 있는 폭을 넓힌다. 남은 강화가 그보다 적으면 있는 만큼만 뜬다.
        int wanted = RelicManager.Instance != null
            ? RelicManager.Instance.UpgradeOptionCount(DefaultOptionCount)
            : DefaultOptionCount;
        wanted = Mathf.Clamp(wanted, 1, MaxOptionCount);

        List<MoveUpgradeOption> rolled = playerMoves.RollUpgrades(wanted);
        if (rolled.Count == 0) return false;

        moves = playerMoves;
        shown.Clear();
        shown.AddRange(rolled);

        // 실제로 뜬 칸 수에 맞춰 패널을 줄인다. 빈 칸이 남으면 아래가 휑하게 벌어진다.
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight(shown.Count));

        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < shown.Count;
            cards[i].gameObject.SetActive(used);
            if (!used) continue;
            cardTexts[i].text = (i + 1) + ".  " + shown[i].title + " — " + shown[i].detail;
            Highlight(cardImages[i], cardTexts[i], false);
        }

        if (hintText != null)
        {
            string keys = "1";
            for (int i = 1; i < shown.Count; i++) keys += " · " + (i + 1);
            hintText.text = "클릭 또는 " + keys + " 키로 선택";
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

        // 칸이 바뀐 순간에만 커서음을 낸다. 판단은 PmdUi가 맡는다 —
        // 창마다 hover 판정을 따로 돌리는 구조라(이 씬에는 EventSystem이 없다)
        // 그 규칙까지 창마다 흩어지면 창을 하나 더 만들 때마다 다시 정하게 된다.
        lastHovered = PmdUi.TrackHoverSound(lastHovered, hovered);

        for (int i = 0; i < shown.Count; i++)
            Highlight(cardImages[i], cardTexts[i], i == hovered);

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
        else if (kb.digit4Key.wasPressedThisFrame && shown.Count > 3) Choose(3);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= shown.Count) return;
        MoveUpgradeOption option = shown[index];
        if (moves != null) moves.ApplyUpgrade(option.id);
        Close(true);
        GameAudio.PlayMoveLearned();

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
