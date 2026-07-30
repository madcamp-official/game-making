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

    /// <summary>
    /// 카드 위 그림을 원본 픽셀의 몇 배로 그릴지.
    ///
    /// ⚠️ <b>칸에 맞춰 늘리지 않고 배율을 못 박는다.</b> 예전에는 160×160 칸에
    /// <see cref="Image.preserveAspect"/>로 채웠는데, 그러면 원본이 작을수록 더 크게 확대된다.
    /// 이상해씨·파이리의 대기 프레임은 32×40이라 4배(128×160)로 들어갔지만 꼬부기는 32×32라
    /// 5배(160×160)가 되어, 꼬부기만 한 눈에 알아볼 만큼 컸다. 같은 배율로 그려야 세 마리가
    /// 같은 세상에 사는 것처럼 보인다.
    ///
    /// 정수배인 것도 중요하다 — 픽셀 아트를 1.25배 같은 비율로 늘리면 획 굵기가 들쭉날쭉해진다.
    /// </summary>
    private const float PreviewPixelScale = 4f;

    /// <summary>그림이 아직 없는 캐릭터의 자리를 대신 잡아 주는 크기.</summary>
    private static readonly Vector2 FallbackPreviewSize = new Vector2(128f, 160f);

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
            Place(card.portrait.rectTransform, new Vector2(0f, 30f), PreviewSize(data.portrait));

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
            CharacterData playable = card.data.ResolvePlayable();
            // "이름 : 설명" 꼴로 적는다. 줄표는 이름과 설명이 대등한 두 토막처럼 보이는데,
            // 여기는 이름이 항목이고 설명이 그 내용이라 쌍점이 관계를 바로 읽힌다.
            styleLine.text = playable != null && playable != card.data
                ? card.data.displayName + " : 준비 중 — 현재 " + playable.displayName + "로 시작"
                : card.data.displayName + " : " + card.data.playStyle;
            startButton.enabled = true;
            Refresh();
            return;
        }
    }

    /// <summary>
    /// 그림 한 장이 차지할 칸 크기. 원본 픽셀 크기에 <see cref="PreviewPixelScale"/>을 곱한다.
    ///
    /// 한 계열의 대기 프레임은 모두 같은 크기로 잘려 있으므로, 마우스를 올려 걷기 시작해
    /// 프레임이 바뀌어도 칸을 다시 잡을 필요가 없다.
    /// </summary>
    private static Vector2 PreviewSize(Sprite sprite)
    {
        if (sprite == null) return FallbackPreviewSize;
        return sprite.rect.size * PreviewPixelScale;
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
