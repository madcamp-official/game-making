using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 관리: 골드, 현재 방, 상호작용 힌트, 중앙 메시지.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private Text goldText;
    [SerializeField] private Text roomText;
    [SerializeField] private Text hintText;
    [SerializeField] private Text messageText;
    [SerializeField] private RectTransform relicBar;

    private Coroutine messageRoutine;
    private RelicTooltip relicTooltip;
    private RelicPopup relicPopup;
    private MoveUpgradePanel upgradePanel;
    private RelicChoicePanel relicChoicePanel;
    private EventDialogue eventDialogue;

    /// <summary>방을 정리했을 때 뜨는 큰 글씨. <see cref="RoomClearSequence"/>가 쓴다.</summary>
    public StageClearBanner StageClear { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnGoldChanged += SetGold;
            SetGold(RunManager.Instance.Gold);
        }
        BuildRelicUi();
        BuildHintPanel();
        if (RelicManager.Instance != null)
        {
            RelicManager.Instance.OnRelicsChanged += RefreshRelics;
            RefreshRelics();
        }
        SetHint("");
        if (messageText != null) messageText.text = "";
    }

    private void RefreshRelics()
    {
        if (relicBar == null || RelicManager.Instance == null) return;

        for (int i = relicBar.childCount - 1; i >= 0; i--)
            Destroy(relicBar.GetChild(i).gameObject);

        if (relicTooltip != null) relicTooltip.ClearTargets();

        int index = 0;
        foreach (RelicData relic in RelicManager.Instance.Relics)
        {
            GameObject iconGo = new GameObject("Relic_" + relic.relicName);
            iconGo.transform.SetParent(relicBar, false);
            Image image = iconGo.AddComponent<Image>();
            image.sprite = relic.icon;
            image.preserveAspect = true;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(56, 56);
            rt.anchoredPosition = new Vector2(-index * 64, 0);
            index++;

            if (relicTooltip != null) relicTooltip.AddTarget(rt, relic);
        }
    }

    /// <summary>플레이어 체력바, 유물 획득 팝업, 호버 툴팁을 캔버스 아래에 만들어 둔다.</summary>
    private void BuildRelicUi()
    {
        Transform canvasRoot = relicBar != null ? relicBar.parent : transform;

        PlayerHealthHud.Create(canvasRoot);
        ExpBar.Create(canvasRoot);
        MoveSlotsHud.Create(canvasRoot);
        relicPopup = MakeFullScreenChild(canvasRoot, "RelicPopup").AddComponent<RelicPopup>();

        // 팔레트·대사창보다 먼저 만든다. 뒤에 만드는 것들이 SetAsLastSibling으로 위에 얹히므로,
        // 글씨가 미처 안 지워진 순간에도 고르는 창을 가리지 않는다.
        StageClear = StageClearBanner.Create(canvasRoot);

        // 툴팁은 다른 HUD 요소 위에 그려져야 한다.
        GameObject tooltipGo = MakeFullScreenChild(canvasRoot, "RelicTooltip");
        tooltipGo.transform.SetAsLastSibling();
        relicTooltip = tooltipGo.AddComponent<RelicTooltip>();

        // 강화 팔레트와 이벤트 대사창은 모든 HUD 위에 뜬다.
        GameObject upgradeGo = MakeFullScreenChild(canvasRoot, "MoveUpgradePanel");
        upgradeGo.transform.SetAsLastSibling();
        upgradePanel = upgradeGo.AddComponent<MoveUpgradePanel>();

        GameObject choiceGo = MakeFullScreenChild(canvasRoot, "RelicChoicePanel");
        choiceGo.transform.SetAsLastSibling();
        relicChoicePanel = choiceGo.AddComponent<RelicChoicePanel>();

        GameObject eventGo = MakeFullScreenChild(canvasRoot, "EventDialogue");
        eventGo.transform.SetAsLastSibling();
        eventDialogue = eventGo.AddComponent<EventDialogue>();

        // 보스 보상 흐름은 가장 위에 뜬다. 유물 선택 창을 띄우는 동안에는 스스로 접으므로
        // 둘이 겹쳐 보이지는 않는다.
        BossRewardSequence.Create(canvasRoot).transform.SetAsLastSibling();
    }

    /// <summary>레벨이 올랐을 때 기술 강화 팔레트를 띄운다.</summary>
    public bool ShowMoveUpgrades(PlayerMoves moves) =>
        upgradePanel != null && upgradePanel.Open(moves);

    /// <summary>보스 보상 유물 둘 중 하나를 고르게 한다 (다우징머신).</summary>
    public bool ShowRelicChoice(RelicData first, RelicData second, System.Action<RelicData> chosen) =>
        relicChoicePanel != null && relicChoicePanel.Open(first, second, chosen);

    /// <summary>이벤트 대사창과 선택지를 띄운다. 다 끝나면 <paramref name="onClosed"/>가 불린다.</summary>
    public bool ShowEvent(EventPrompt prompt, System.Action onClosed) =>
        eventDialogue != null && eventDialogue.Open(prompt, onClosed);

    /// <summary>화면 전체를 덮는 빈 컨테이너. 안쪽 패널이 화면 기준으로 배치될 수 있게 한다.</summary>
    private static GameObject MakeFullScreenChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    /// <summary>유물 획득 시 전용 패널로 이름과 설명을 크게 보여준다.</summary>
    public void ShowRelicAcquired(RelicData relic, float duration)
    {
        if (relicPopup != null) relicPopup.Show(relic, duration);
    }

    private int lastGold = int.MinValue;
    private string lastHint;
    private RectTransform hintPanel;

    /// <summary>상호작용 안내 창의 자리와 최대 폭. 좌하단 체력바와 우하단 기술 칸을 비켜 앉는다.</summary>
    private const float HintBottom = 56f;
    private const float HintMaxWidth = 1080f;
    private const float HintSideMargin = 420f;

    public void SetGold(int gold)
    {
        if (goldText == null || gold == lastGold) return;
        lastGold = gold;
        goldText.text = "G " + gold;
    }

    public void SetRoomName(string label)
    {
        if (roomText != null) roomText.text = label;
    }

    // 같은 힌트가 유지되는 동안 Text 재대입(캔버스 리빌드)을 피한다.
    public void SetHint(string hint)
    {
        if (hintText == null || hint == lastHint) return;
        lastHint = hint;
        hintText.text = hint;
        // 상점 상품 설명이 여기로 온다. 맨바닥 글자로 두면 방 바닥 무늬에 묻히므로
        // 대화창에 담고, 할 말이 없을 때는 창까지 함께 치운다.
        if (hintPanel == null) return;
        bool show = !string.IsNullOrEmpty(hint);
        hintPanel.gameObject.SetActive(show);
        if (show) LayoutHintPanel();
    }

    /// <summary>
    /// 아래쪽 상호작용 안내를 대화창에 담는다.
    ///
    /// 씬의 Text를 새로 만들지 않고 <b>창 안으로 옮긴다</b> — 이 Text는 씬에서 물려 준
    /// 참조라 다시 만들면 연결이 끊긴다. 부모를 바꾸는 것은 참조를 건드리지 않는다.
    ///
    /// 옮기면서 줄바꿈을 켜는 것이 핵심이다. 유물 상품 설명은 한 줄로 화면 폭을 넘어가서
    /// (예: "구매 (400G) — 이동 속도가 20% 증가하는 대신, 근접·원거리 공격의 피해량이...")
    /// 창만 씌우면 글자가 창을 뚫고 화면 밖으로 흘러나간다.
    /// </summary>
    private void BuildHintPanel()
    {
        if (hintText == null) return;

        RectTransform textRt = hintText.rectTransform;
        hintPanel = PmdUi.MakePanel(textRt.parent, "HintPanel").rectTransform;
        hintPanel.anchorMin = hintPanel.anchorMax = new Vector2(0.5f, 0f);
        hintPanel.pivot = new Vector2(0.5f, 0f);
        hintPanel.anchoredPosition = new Vector2(0f, HintBottom);
        hintPanel.SetSiblingIndex(textRt.GetSiblingIndex());

        textRt.SetParent(hintPanel, false);
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        hintText.verticalOverflow = VerticalWrapMode.Overflow;
        PmdUi.Stretch(textRt);
        textRt.offsetMin = new Vector2(PmdUi.PanelInset.x, PmdUi.PanelInset.y);
        textRt.offsetMax = new Vector2(-PmdUi.PanelInset.x, -PmdUi.PanelInset.y);

        hintPanel.gameObject.SetActive(false);
    }

    /// <summary>창을 글자 줄 수에 맞춰 키운다. 폭은 좌우 HUD를 침범하지 않는 선에서 잡는다.</summary>
    private void LayoutHintPanel()
    {
        var canvasRect = hintPanel.parent as RectTransform;
        float available = canvasRect != null ? canvasRect.rect.width : HintMaxWidth;
        float width = Mathf.Min(HintMaxWidth, available - HintSideMargin * 2f);
        // 줄 수는 줄바꿈 폭이 정해진 뒤에야 올바르게 나온다. 배치 전에는 rect가 0이라
        // 폭을 직접 넘겨야 한 글자에 한 줄씩 세지 않는다.
        float inner = width - PmdUi.PanelInset.x * 2f;
        float lines = PixelUi.LineBoxHeight(hintText, inner);
        hintPanel.sizeDelta = new Vector2(width, lines + PmdUi.PanelInset.y * 2f + 8f);
    }

    public void ShowMessage(string message, float duration)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(message, duration));
    }

    private IEnumerator MessageRoutine(string message, float duration)
    {
        messageText.text = message;
        // 실제 시간으로 센다. 진화 연출과 보상 화면은 시간을 멈춘 채로 도는데,
        // 스케일 시간으로 기다리면 그동안 띄운 안내가 흐름 내내 화면에 눌어붙는다.
        yield return new WaitForSecondsRealtime(duration);
        messageText.text = "";
        messageRoutine = null;
    }
}
