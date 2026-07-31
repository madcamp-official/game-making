using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임에 들어가기 전 조작·기본 기술 안내.
///
/// 첫 플레이에는 저절로 뜨고, <b>다시 보지 않기</b>를 고르면 그 뒤로는 캐릭터를 고르는 즉시
/// 게임이 시작된다(<see cref="GameFlow.SkipGuide"/>).
///
/// <b>들어온 길에 따라 다른 화면이 된다.</b>
///
/// <list type="bullet">
/// <item>캐릭터를 고르고 온 길 — 조작 방법 다음에 시작 기술 둘을 보여 주고 게임으로 들어간다</item>
/// <item>타이틀의 '조작 방법'으로 온 길 — 시작할 캐릭터가 없다. "뒤로가기" 하나뿐이다</item>
/// </list>
///
/// 둘을 가르지 않았을 때는 타이틀에서 구경하러 들어와도 "시작"이 보여서, 그것을 누르면
/// 캐릭터도 고르지 않은 채 판이 시작되는 것처럼 읽혔다.
///
/// 배경은 <b>불투명하게</b> 깐다. 예전에는 옅게 덮기만 해서 뒤의 게임 세상과 HUD가 비쳐
/// 보였고, 타이틀에서 조작만 보러 들어왔는데도 <b>게임이 이미 시작된 것처럼</b> 보였다.
/// 이 화면은 세상 위에 얹는 덮개가 아니라 그 자체로 한 장면이다.
/// </summary>
public class ControlsGuideScreen : FlowScreen
{
    private const int SortingOrder = 610;

    private CharacterData character;

    /// <summary>일시정지 메뉴에서 구경하러 온 길인지. 뒤로가기가 타이틀 대신 그 메뉴로 돌아간다.</summary>
    private bool fromPause;

    public static ControlsGuideScreen Open(GameFlow flow, CharacterData character, bool fromPause = false)
    {
        // ⚠️ 캐릭터는 반드시 Build보다 먼저 넣는다. Build가 이 값으로 버튼을 정하는데,
        // 돌려받은 뒤에 넣으면 Build는 언제나 빈 값을 본다.
        var screen = Create<ControlsGuideScreen>(flow, "ControlsGuideScreen", SortingOrder,
            s => { s.character = character; s.fromPause = fromPause; });
        screen.FillBody();
        return screen;
    }

    private Text actionColumn;
    private Text keyColumn;
    private Image controlsPanel;
    private RectTransform movesRoot;
    private PmdUi.Entry nextButton;
    private PmdUi.Entry skipToggle;
    private PmdUi.Entry startButton;

    /// <summary>진화 뒤 새 기술 안내와 같은 정보 계층을 가진 기술 카드 한 장.</summary>
    private class MoveCard
    {
        public RectTransform panel;
        public Text header;
        public Text title;
        public Text body;
    }

    private const int MovePanelWidth = 660;
    private const int MovePanelPadding = 20;
    private const float MovePanelGap = 16f;

    /// <summary>
    /// 안내 줄의 자리. 동작 이름과 키를 <b>서로 다른 글자 상자</b>에 나눠 담아 키가 언제나
    /// 같은 열에서 시작하게 한다.
    ///
    /// 예전에는 한 상자에 공백을 채워 맞췄다. PMD 비트맵 폰트는 한글 한 칸이 공백 셋 폭이라
    /// 글자 수 대신 폭을 세어 맞췄는데, 그 3:1이 모든 글리프에 정확히 들어맞지는 않아서
    /// 줄마다 키가 한두 픽셀씩 어긋났다 — 좌클릭과 우클릭처럼 나란한 줄에서 특히 눈에 띈다.
    /// 상자를 나누면 맞추는 계산 자체가 없어진다.
    /// </summary>
    private const float ActionLeft = -300f;
    private const float ActionWidth = 220f;
    private const float KeyLeft = ActionLeft + ActionWidth;
    private const float KeyWidth = 380f;
    private const int BodyFontSize = 26;

    /// <summary>타이틀에서 구경하러 들어왔는가. 시작할 캐릭터가 없으면 그 길이다.</summary>
    private bool FromTitle => character == null;

    protected override void Build()
    {
        // 타이틀과 같은 밤하늘빛 판. 알파를 두지 않아 뒤의 게임 화면이 비치지 않는다.
        var background = new GameObject("Background", typeof(RectTransform));
        background.transform.SetParent(Root, false);
        var bg = background.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.14f);
        PmdUi.Stretch(bg.rectTransform);

        controlsPanel = PmdUi.MakePanel(Root, "Panel");
        Place(controlsPanel.rectTransform, new Vector2(0f, 40f), new Vector2(760f, 420f));

        Text heading = PmdUi.MakeText(controlsPanel.rectTransform, "Heading", "조작 방법", 34);
        heading.color = PmdUi.AccentColor;
        Place(heading.rectTransform, new Vector2(0f, 160f), new Vector2(700f, 50f));

        actionColumn = PmdUi.MakeText(controlsPanel.rectTransform, "Actions", "", BodyFontSize,
                                      TextAnchor.UpperLeft);
        PlaceLeft(actionColumn.rectTransform, ActionLeft, 110f, ActionWidth, 260f);

        keyColumn = PmdUi.MakeText(controlsPanel.rectTransform, "Keys", "", BodyFontSize,
                                   TextAnchor.UpperLeft);
        PlaceLeft(keyColumn.rectTransform, KeyLeft, 110f, KeyWidth, 260f);

        if (FromTitle || fromPause)
        {
            // 구경하러 온 길에는 갈 곳이 하나다. "다시 보지 않기"는 게임을 시작하는 길에서만
            // 뜻이 있는 설정이라 여기서는 내보내지 않는다.
            entries.Add(PmdUi.MakeEntry(Root, "Back", "뒤로가기", 28,
                new Vector2(0f, -230f), new Vector2(260f, 52f)));
            cursor = 0;
            return;
        }

        nextButton = PmdUi.MakeEntry(Root, "Next", "다음", 28,
            new Vector2(0f, -230f), new Vector2(240f, 52f));
        entries.Add(nextButton);

        // 두 번째 장. 진화 뒤 새 기술을 배울 때의 카드 두 장을 세로로 잇는다.
        movesRoot = PmdUi.MakeFullScreen(Root, "StartingMoves");
        float moveButtonsY = BuildMoveGuide();

        // 다시 보지 않기 — 두 기술을 모두 본 뒤에만 고를 수 있다.
        skipToggle = PmdUi.MakeEntry(movesRoot, "SkipToggle", "", 22,
            new Vector2(-190f, moveButtonsY), new Vector2(340f, 52f));
        entries.Add(skipToggle);

        startButton = PmdUi.MakeEntry(movesRoot, "Continue", "시작", 28,
            new Vector2(190f, moveButtonsY), new Vector2(240f, 52f));
        entries.Add(startButton);

        movesRoot.gameObject.SetActive(false);
        skipToggle.enabled = false;
        startButton.enabled = false;
        cursor = 0;
        UpdateSkipLabel();
    }

    /// <summary>
    /// 선택한 캐릭터의 시작 기술 둘을 보상 화면의 기술 안내와 같은 형식으로 쌓는다.
    /// 캐릭터 선택 카드가 준비 중인 폴백을 가리키는 경우에는 실제 플레이할 캐릭터의 기술을 쓴다.
    /// </summary>
    private float BuildMoveGuide()
    {
        CharacterData playable = character != null ? character.ResolvePlayable() : null;
        PlayerMoveSet moveSet = playable != null ? playable.moveSet : null;
        if (moveSet == null) return -230f;

        int count = Mathf.Min(2, moveSet.StartingCount);
        if (count <= 0) return -230f;

        var cards = new MoveCard[count];
        float width = Mathf.Min(MovePanelWidth, Mathf.Max(280f, Screen.width - MovePanelPadding * 2f));
        float totalHeight = 0f;

        for (int i = 0; i < count; i++)
        {
            PlayerMoveDefinition definition = moveSet.DefinitionAt(i);
            if (definition == null) continue;

            cards[i] = MakeMoveCard(i);
            string tag = MoveInfo.TagOf(definition.type, moveSet);
            string details = "조작 : " + MoveInfo.KeyLabelOf(definition.type, moveSet);
            if (!string.IsNullOrEmpty(tag)) details += "    속성 : " + tag;

            cards[i].header.text = "기본 기술 " + (i + 1);
            cards[i].title.text = MoveInfo.NameOf(definition.type, moveSet);
            cards[i].body.text = details + "\n" + MoveInfo.SummaryOf(definition.type, moveSet);

            float height = LayoutMoveCard(cards[i], width);
            totalHeight += height;
            if (i + 1 < count) totalHeight += MovePanelGap;
        }

        float top = Mathf.Min(300f, totalHeight * 0.5f + 82f);
        float y = top;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].panel.anchoredPosition = new Vector2(0f, y);
            y -= cards[i].panel.sizeDelta.y + MovePanelGap;
        }

        // 마지막 카드 바로 아래에 두 버튼을 둔다. 설명 길이가 달라도 간격이 유지된다.
        return y - 30f;
    }

    private MoveCard MakeMoveCard(int index)
    {
        var card = new MoveCard();
        card.panel = PixelUi.MakePanel(movesRoot, "Move" + (index + 1));
        card.panel.anchorMin = card.panel.anchorMax = new Vector2(0.5f, 0.5f);
        card.panel.pivot = new Vector2(0.5f, 1f);

        Transform fill = card.panel.GetChild(0);
        card.header = PixelUi.MakeText(fill, "Header", 24, new Color(0.72f, 0.78f, 0.88f),
                                       TextAnchor.UpperCenter);
        card.title = PixelUi.MakeText(fill, "Title", 36, new Color(1f, 0.86f, 0.42f),
                                      TextAnchor.UpperCenter);
        card.body = PixelUi.MakeText(fill, "Body", 24, Color.white, TextAnchor.UpperCenter);
        return card;
    }

    private static float LayoutMoveCard(MoveCard card, float width)
    {
        card.panel.sizeDelta = new Vector2(width, card.panel.sizeDelta.y);

        float gap = MovePanelPadding * 0.5f;
        float y = -MovePanelPadding;
        y = PixelUi.StackFromTop(card.header, y, MovePanelPadding) - gap * 0.5f;
        y = PixelUi.StackFromTop(card.title, y, MovePanelPadding) - gap;
        y = PixelUi.StackFromTop(card.body, y, MovePanelPadding);

        float height = -y + MovePanelPadding;
        card.panel.sizeDelta = new Vector2(width, height);
        return height;
    }

    private void FillBody()
    {
        // 기술 이름은 적지 않는다. 캐릭터마다 다르고 진화하며 늘어나는데, 여기는 "어느 키가
        // 몇 번째 기술인가"만 알려 주면 되는 자리다. 이름은 기술 칸 HUD와 습득 화면이 맡는다.
        //
        // 두 칸에 같은 줄 수를 같은 차례로 넣는다. 줄 높이가 같으니 n번째 줄끼리 저절로 나란해진다.
        var actions = new System.Text.StringBuilder();
        var keys = new System.Text.StringBuilder();

        void Row(string label, string key)
        {
            actions.Append(label).Append('\n');
            keys.Append(key).Append('\n');
        }

        Row("이동", "WASD / 방향키");
        for (int i = 0; i < MoveInfo.MaxMoves; i++)
            Row("기술 " + (i + 1), MoveInfo.KeyLabelForSlot(i));
        Row("상호작용", "E");
        Row("일시정지", "Esc");

        actionColumn.text = actions.ToString().TrimEnd('\n');
        keyColumn.text = keys.ToString().TrimEnd('\n');
    }

    private void UpdateSkipLabel()
    {
        skipToggle.label.text = (Flow.SkipGuide ? "[v] " : "[  ] ") + "다시 보지 않기";
    }

    protected override void Activate(int index)
    {
        if (nextButton != null && entries[index] == nextButton)
        {
            ShowMoveGuide();
            return;
        }

        if (skipToggle != null && entries[index] == skipToggle)
        {
            Flow.SkipGuide = !Flow.SkipGuide;
            UpdateSkipLabel();
            return;
        }

        // 온 길로 되돌아간다 — 일시정지에서 왔으면 그 메뉴로, 타이틀에서 구경만 왔으면
        // 타이틀로, 캐릭터를 고르고 왔으면 게임으로.
        if (fromPause) Flow.OpenPauseMenu();
        else if (FromTitle) Flow.GoTitle();
        else Flow.BeginRun();
    }

    private void ShowMoveGuide()
    {
        controlsPanel.gameObject.SetActive(false);
        nextButton.panel.gameObject.SetActive(false);
        nextButton.enabled = false;

        movesRoot.gameObject.SetActive(true);
        skipToggle.enabled = true;
        startButton.enabled = true;
        cursor = entries.IndexOf(startButton);
        UpdateSkipLabel();
        Refresh();
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    /// <summary>왼쪽 위 끝을 기준으로 놓는다. 두 칸이 같은 열에서 시작하려면 이쪽이라야 한다.</summary>
    private static void PlaceLeft(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(left, top);
    }
}
