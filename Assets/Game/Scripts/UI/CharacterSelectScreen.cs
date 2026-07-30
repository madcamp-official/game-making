using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면. 마우스만으로 세 캐릭터 중 하나를 고른다.
///
/// 마우스를 올리면 그 캐릭터가 걷기 시작하고 창이 밝아진다. 누르면 테두리가 노랗게 남고
/// 아래에 플레이 스타일 한 줄이 붙는다. <b>수치는 적지 않는다</b> — 진화·강화·유물로 계속
/// 달라져서, 적어 두면 화면이 곧 거짓말을 한다.
///
/// 고르기 전에는 시작 버튼을 눌러도 아무 일이 없게 꺼 둔다.
/// </summary>
public class CharacterSelectScreen : FlowScreen
{
    private const int SortingOrder = 610;

    private class Card
    {
        public CharacterData data;
        public PmdUi.Entry entry;
        public Image portrait;
        public Animator animator;
        public Text nameLabel;
    }

    private readonly List<Card> cards = new List<Card>();
    private PmdUi.Entry startButton;
    private PmdUi.Entry backButton;
    private Text styleLine;
    private CharacterData chosen;

    public static CharacterSelectScreen Open(GameFlow flow) =>
        Create<CharacterSelectScreen>(flow, "CharacterSelectScreen", SortingOrder);

    protected override void Build()
    {
        PmdUi.MakeBackdrop(Root, "Backdrop", 0.92f);

        Text heading = PmdUi.MakeText(Root, "Heading", "함께 갈 포켓몬을 고르세요", 36);
        heading.color = PmdUi.AccentColor;
        Place(heading.rectTransform, new Vector2(0f, 260f), new Vector2(900f, 56f));

        IReadOnlyList<CharacterData> list = Flow.Characters;
        int count = list != null ? list.Count : 0;
        float spacing = 280f;
        float startX = -(count - 1) * spacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            CharacterData data = list[i];
            if (data == null) continue;

            var card = new Card { data = data };
            card.entry = PmdUi.MakeEntry(Root, "Card" + i, "", 24,
                new Vector2(startX + i * spacing, 60f), new Vector2(240f, 300f));

            // 그림 — 창 안쪽에 크게. 없으면 이름만 남는다.
            var portraitGo = new GameObject("Portrait", typeof(RectTransform));
            portraitGo.transform.SetParent(card.entry.rect, false);
            card.portrait = portraitGo.AddComponent<Image>();
            card.portrait.sprite = data.portrait;
            card.portrait.preserveAspect = true;
            card.portrait.enabled = data.HasPortrait;
            Place(card.portrait.rectTransform, new Vector2(0f, 30f), new Vector2(160f, 160f));

            if (data.previewController != null)
            {
                card.animator = portraitGo.AddComponent<Animator>();
                card.animator.runtimeAnimatorController = data.previewController;
                // timeScale이 0이라 보통 애니메이터는 멈춘다. 실제 시간으로 돌려 준다.
                card.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            card.nameLabel = PmdUi.MakeText(card.entry.rect, "Name", data.displayName, 28);
            Place(card.nameLabel.rectTransform, new Vector2(0f, -110f), new Vector2(220f, 44f));

            cards.Add(card);
            entries.Add(card.entry);
        }

        styleLine = PmdUi.MakeText(Root, "StyleLine", "", 26);
        Place(styleLine.rectTransform, new Vector2(0f, -140f), new Vector2(900f, 46f));

        backButton = PmdUi.MakeEntry(Root, "Back", "뒤로", 28,
            new Vector2(-160f, -240f), new Vector2(260f, 56f));
        startButton = PmdUi.MakeEntry(Root, "Start", "게임 시작", 28,
            new Vector2(160f, -240f), new Vector2(260f, 56f));
        startButton.enabled = false;   // 고르기 전에는 누를 수 없다
        entries.Add(backButton);
        entries.Add(startButton);
        cursor = -1;
    }

    /// <summary>마우스가 올라간 카드만 걸어 준다. 나머지는 가만히 서 있는다.</summary>
    protected override void OnCursorChanged()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card.animator == null) continue;
            bool hovered = cursor >= 0 && cursor < entries.Count && entries[cursor] == card.entry;
            string state = hovered ? card.data.previewHoverState : card.data.previewIdleState;
            if (string.IsNullOrEmpty(state)) continue;
            if (card.animator.HasState(0, Animator.StringToHash(state)))
                card.animator.Play(state);
        }

        // 고른 카드는 이름을 노랗게 남겨 둔다 — 마우스를 치워도 무엇을 골랐는지 보여야 한다.
        foreach (Card card in cards)
            card.nameLabel.color = card.data == chosen ? PmdUi.HighlightColor : PmdUi.TextColor;
    }

    protected override void Activate(int index)
    {
        PmdUi.Entry entry = entries[index];

        if (entry == backButton) { Flow.GoTitle(); return; }
        if (entry == startButton)
        {
            if (chosen != null) Flow.ChooseCharacter(chosen);
            return;
        }

        foreach (Card card in cards)
        {
            if (card.entry != entry) continue;
            chosen = card.data;
            styleLine.text = card.data.displayName + " — " + card.data.playStyle;
            startButton.enabled = true;
            Refresh();
            return;
        }
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
