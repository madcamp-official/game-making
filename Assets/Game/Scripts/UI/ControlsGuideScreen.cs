using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임에 들어가기 전 조작 안내. 한 화면에 짧게 보여 주고 바로 들어간다.
///
/// 첫 플레이에는 저절로 뜨고, <b>다시 보지 않기</b>를 고르면 그 뒤로는 캐릭터를 고르는 즉시
/// 게임이 시작된다(<see cref="GameFlow.SkipGuide"/>). 타이틀의 '조작 방법'으로 들어온
/// 경우에는 시작할 캐릭터가 없으므로 타이틀로 돌아간다.
/// </summary>
public class ControlsGuideScreen : FlowScreen
{
    private const int SortingOrder = 610;

    private CharacterData character;

    public static ControlsGuideScreen Open(GameFlow flow, CharacterData character)
    {
        var screen = Create<ControlsGuideScreen>(flow, "ControlsGuideScreen", SortingOrder);
        screen.character = character;
        screen.FillBody();
        return screen;
    }

    private Text body;
    private Text characterLine;
    private PmdUi.Entry skipToggle;

    protected override void Build()
    {
        PmdUi.MakeBackdrop(Root, "Backdrop", 0.9f);

        Image panel = PmdUi.MakePanel(Root, "Panel");
        Place(panel.rectTransform, new Vector2(0f, 40f), new Vector2(760f, 420f));

        Text heading = PmdUi.MakeText(panel.rectTransform, "Heading", "조작 방법", 34);
        heading.color = PmdUi.AccentColor;
        Place(heading.rectTransform, new Vector2(0f, 160f), new Vector2(700f, 50f));

        body = PmdUi.MakeText(panel.rectTransform, "Body", "", 26, TextAnchor.UpperLeft);
        Place(body.rectTransform, new Vector2(0f, -10f), new Vector2(640f, 240f));

        characterLine = PmdUi.MakeText(panel.rectTransform, "CharacterLine", "", 22);
        characterLine.color = PmdUi.HighlightColor;
        Place(characterLine.rectTransform, new Vector2(0f, -160f), new Vector2(700f, 40f));

        // 다시 보지 않기 — 지금 상태를 글자로 보여 주고, 누르면 뒤집는다.
        skipToggle = PmdUi.MakeEntry(Root, "SkipToggle", "", 22,
            new Vector2(-190f, -230f), new Vector2(340f, 52f));
        entries.Add(skipToggle);

        entries.Add(PmdUi.MakeEntry(Root, "Continue", "시작", 28,
            new Vector2(190f, -230f), new Vector2(240f, 52f)));
        cursor = 1;
        UpdateSkipLabel();
    }

    private void FillBody()
    {
        body.text =
            "이동            WASD / 방향키\n" +
            "기본 공격      좌클릭\n" +
            "특수 공격      우클릭 · Shift · Space\n" +
            "상호작용      E\n" +
            "일시정지      Esc";

        characterLine.text = character != null
            ? character.displayName + " — " + character.playStyle
            : "";
    }

    private void UpdateSkipLabel()
    {
        skipToggle.label.text = (Flow.SkipGuide ? "[v] " : "[  ] ") + "다시 보지 않기";
    }

    protected override void Activate(int index)
    {
        if (entries[index] == skipToggle)
        {
            Flow.SkipGuide = !Flow.SkipGuide;
            UpdateSkipLabel();
            return;
        }

        // 캐릭터를 고르고 온 길이면 게임으로, 타이틀에서 구경만 온 길이면 타이틀로.
        if (character != null) Flow.BeginRun();
        else Flow.GoTitle();
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
