using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면. 배경 그림이 들어갈 자리를 비워 두고, 그 위에 제목과 메뉴를 얹는다.
///
/// 여기서 바로 캐릭터를 고르게 하지 않는다. 타이틀과 캐릭터 선택을 나눠 두면 설정·크레딧·
/// 도감 같은 메뉴가 늘어날 자리가 생기고, 캐릭터를 고르는 일도 판의 첫 선택처럼 무게를 갖는다.
///
/// 한 번이라도 플레이했다면 맨 위에 <b>최근 캐릭터로 빠른 시작</b>이 붙는다. 같은 캐릭터로
/// 계속 도는 사람이 고르는 화면을 매번 거치지 않아도 되게 하는 것이다.
/// </summary>
public class TitleScreen : FlowScreen
{
    private const int SortingOrder = 600;

    /// <summary>어떤 칸이 무슨 일을 하는지. 빠른 시작이 있으면 한 칸씩 밀린다.</summary>
    private enum Command { QuickStart, Start, Controls, Settings, Credits, Quit }

    private readonly System.Collections.Generic.List<Command> commands =
        new System.Collections.Generic.List<Command>();

    private Text notice;

    public static TitleScreen Open(GameFlow flow) =>
        Create<TitleScreen>(flow, "TitleScreen", SortingOrder);

    protected override void Build()
    {
        // 배경 — 그림이 들어올 자리다. 지금은 밤하늘빛으로 채워 두고 이름만 남긴다.
        var background = new GameObject("Background", typeof(RectTransform));
        background.transform.SetParent(Root, false);
        var bg = background.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.14f);
        PmdUi.Stretch(bg.rectTransform);

        Text title = PmdUi.MakeText(Root, "Title", "이상해씨의 던전 탐험", 84);
        title.color = PmdUi.AccentColor;
        Place(title.rectTransform, new Vector2(0f, 250f), new Vector2(900f, 110f));

        Text subtitle = PmdUi.MakeText(Root, "Subtitle", "포켓몬 로그라이트", 26);
        Place(subtitle.rectTransform, new Vector2(0f, 180f), new Vector2(900f, 40f));

        commands.Clear();
        if (Flow.HasPlayedBefore && Flow.LastCharacter != null) commands.Add(Command.QuickStart);
        commands.Add(Command.Start);
        commands.Add(Command.Controls);
        commands.Add(Command.Settings);
        commands.Add(Command.Credits);
        commands.Add(Command.Quit);

        float y = 90f;
        for (int i = 0; i < commands.Count; i++)
        {
            entries.Add(PmdUi.MakeEntry(Root, "Menu" + i, LabelOf(commands[i]), 30,
                new Vector2(0f, y), new Vector2(420f, 58f)));
            y -= 68f;
        }
        cursor = 0;

        notice = PmdUi.MakeText(Root, "Notice", "", 22);
        notice.color = PmdUi.DisabledColor;
        Place(notice.rectTransform, new Vector2(0f, y - 30f), new Vector2(900f, 60f));
    }

    private string LabelOf(Command command)
    {
        switch (command)
        {
            case Command.QuickStart:
                CharacterData last = Flow.LastCharacter;
                return "빠른 시작 — " + (last != null ? last.displayName : "");
            case Command.Start: return "게임 시작";
            case Command.Controls: return "조작 방법";
            case Command.Settings: return "설정";
            case Command.Credits: return "크레딧";
            case Command.Quit: return "게임 종료";
        }
        return "";
    }

    protected override void Activate(int index)
    {
        switch (commands[index])
        {
            case Command.QuickStart:
                Flow.ChooseCharacter(Flow.LastCharacter);
                break;
            case Command.Start:
                Flow.GoCharacterSelect();
                break;
            case Command.Controls:
                // 캐릭터를 고르기 전에도 조작은 볼 수 있어야 한다. 보고 나면 타이틀로 돌아온다.
                Flow.GoGuide();
                break;
            case Command.Settings:
                Tell("설정은 아직 준비 중이다.");
                break;
            case Command.Credits:
                Tell("크레딧은 docs/CREDITS.md에 정리해 두었다.");
                break;
            case Command.Quit:
                Quit();
                break;
        }
    }

    private void Tell(string message)
    {
        if (notice != null) notice.text = message;
    }

    /// <summary>
    /// 게임을 끝낸다. 에디터에서는 <see cref="Application.Quit"/>이 아무 일도 하지 않으므로
    /// 재생을 멈춘다 — 눌렀는데 반응이 없으면 고장으로 보인다.
    /// </summary>
    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
