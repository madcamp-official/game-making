using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임에 들어가기 전 조작 안내. 한 화면에 짧게 보여 주고 바로 들어간다.
///
/// 첫 플레이에는 저절로 뜨고, <b>다시 보지 않기</b>를 고르면 그 뒤로는 캐릭터를 고르는 즉시
/// 게임이 시작된다(<see cref="GameFlow.SkipGuide"/>).
///
/// <b>들어온 길에 따라 다른 화면이 된다.</b>
///
/// <list type="bullet">
/// <item>캐릭터를 고르고 온 길 — 다음은 게임이다. "다시 보지 않기"와 "시작"이 붙는다</item>
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

        if (FromTitle)
        {
            // 구경하러 온 길에는 갈 곳이 하나다. "다시 보지 않기"는 게임을 시작하는 길에서만
            // 뜻이 있는 설정이라 여기서는 내보내지 않는다.
            entries.Add(PmdUi.MakeEntry(Root, "Back", "뒤로가기", 28,
                new Vector2(0f, -230f), new Vector2(260f, 52f)));
            cursor = 0;
            return;
        }

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
        if (skipToggle != null && entries[index] == skipToggle)
        {
            Flow.SkipGuide = !Flow.SkipGuide;
            UpdateSkipLabel();
            return;
        }

        // 캐릭터를 고르고 온 길이면 게임으로, 타이틀에서 구경만 온 길이면 타이틀로.
        if (FromTitle) Flow.GoTitle();
        else Flow.BeginRun();
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
