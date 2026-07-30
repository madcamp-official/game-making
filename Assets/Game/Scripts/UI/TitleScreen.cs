using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면. 모든 방이 뒤로 흘러가고(<see cref="TitleMapBackdrop"/>) 그 위에 로고와 메뉴가 얹힌다.
///
/// 여기서 바로 캐릭터를 고르게 하지 않는다. 타이틀과 캐릭터 선택을 나눠 두면 크레딧·도감 같은
/// 메뉴가 늘어날 자리가 생기고, 캐릭터를 고르는 일도 판의 첫 선택처럼 무게를 갖는다.
///
/// 한 번이라도 플레이했다면 맨 위에 <b>지난 캐릭터로 이어서 시작</b>하는 칸이 붙는다. 같은
/// 캐릭터로 계속 도는 사람이 고르는 화면을 매번 거치지 않아도 되게 하는 것이다.
///
/// <b>칸은 셋뿐이다.</b> 예전에는 설정과 게임 종료도 있었는데, 설정은 열면 "아직 준비 중"이라는
/// 말만 하고 닫혔고 — 있는데 아무 일도 하지 않는 칸은 없는 칸보다 나쁘다 — 종료는 창을 닫으면
/// 되는 일에 메뉴 한 줄을 쓰는 셈이었다.
/// </summary>
public class TitleScreen : FlowScreen
{
    private const int SortingOrder = 600;

    /// <summary>어떤 칸이 무슨 일을 하는지. 이어서 시작이 있으면 한 칸씩 밀린다.</summary>
    private enum Command { Continue, Start, Controls, Credits }

    private readonly System.Collections.Generic.List<Command> commands =
        new System.Collections.Generic.List<Command>();

    private Text notice;

    /// <summary>로고가 머무는 높이. 떠다니는 움직임은 이 자리를 기준으로 오간다.</summary>
    private const float LogoY = 246f;

    /// <summary>
    /// 로고를 그림 크기의 몇 배로 놓을지. 1.8배면 423×264가 761×475가 된다.
    ///
    /// 정수배(2배)가 픽셀은 더 깨끗하지만 528픽셀이라 화면 위쪽이 눌렸다. 로고는 획이 굵어
    /// 흔들림이 잘 드러나지 않으므로 크기를 택했다 — 자세한 사정은
    /// <see cref="PmdUi.MakeLogo"/>에 적혀 있다.
    ///
    /// ⚠️ 이 값을 바꾸면 <see cref="LogoY"/>·<see cref="MenuTop"/>을 함께 다시 재야 한다.
    /// 로고 아래끝과 첫 메뉴 칸 윗끝 사이가 40픽셀 남짓뿐이다.
    /// </summary>
    private const float LogoPixelScale = 1.8f;

    /// <summary>
    /// 메뉴 칸의 크기와 글자.
    ///
    /// 타이틀은 이 게임에서 <b>가장 먼저 보는 화면</b>이고 칸이 넷뿐이라, 게임 안의 창들보다
    /// 큼직해도 화면이 비지 않는다. 예전 크기(420×58, 글자 30)는 흐르는 배경 위에서 작은
    /// 표처럼 보였다.
    ///
    /// ⚠️ 글자 크기는 <b>12의 배수</b>여야 한다. PMD 비트맵 폰트라 그 사이 값은
    /// <see cref="PixelUi.SnapFontSize"/>가 반올림해 버리는데, 30은 24로 내려간다(짝수 쪽으로
    /// 반올림) — 키운 줄 알았던 값이 오히려 작아지는 자리다.
    /// </summary>
    private const int MenuFontSize = 36;
    private static readonly Vector2 MenuSize = new Vector2(560f, 78f);

    /// <summary>칸 사이 간격과 첫 칸의 높이. 로고를 키우면서 그만큼 아래로 내렸다.</summary>
    private const float MenuGap = 14f;
    private const float MenuTop = -72f;

    /// <summary>
    /// 로고가 떠다니는 폭과 한 번 오가는 데 걸리는 시간.
    ///
    /// 폭을 정수 픽셀로 두는 것이 중요하다. 로고는 픽셀 아트이고 캔버스는 ConstantPixelSize라,
    /// 소수 자리에 놓이면 획이 두 픽셀에 걸쳐 번진다 — 떠다니는 내내 글자가 지글거린다.
    /// 그래서 <see cref="Mathf.Round"/>로 끊어 올린다.
    /// </summary>
    private const float FloatAmplitude = 8f;
    private const float FloatPeriod = 3.6f;

    private RectTransform logoRect;

    /// <summary>
    /// 로고를 위아래로 살짝 띄운다. 사인이라 끝에서 부드럽게 되돌아온다 — 톱니로 오가면
    /// 방향이 바뀌는 순간이 눈에 걸린다.
    ///
    /// 시간은 실제 시간으로 센다. 타이틀은 <see cref="Time.timeScale"/>이 0인 채로 떠 있다.
    /// </summary>
    private void LateUpdate()
    {
        if (logoRect == null) return;
        float phase = Time.unscaledTime * (Mathf.PI * 2f / FloatPeriod);
        float offset = Mathf.Round(Mathf.Sin(phase) * FloatAmplitude);
        logoRect.anchoredPosition = new Vector2(0f, LogoY + offset);
    }

    public static TitleScreen Open(GameFlow flow) =>
        Create<TitleScreen>(flow, "TitleScreen", SortingOrder);

    protected override void Build()
    {
        // 배경 — 모든 방이 뒤로 흘러간다. 스틸을 굽지 않았으면 예전처럼 단색으로 버틴다.
        TitleMapBackdrop.Create(Root);

        // 제목은 로고 그림이 맡는다. 그림이 없으면 글자로 되돌아간다 — 스프라이트를 아직
        // 들여오지 않은 사람의 화면에서 제목이 통째로 사라지는 것이 가장 나쁘다.
        Image logo = PmdUi.MakeLogo(Root, "Logo", LogoPixelScale);
        if (logo != null)
        {
            logoRect = logo.rectTransform;
            logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            // 크기는 MakeLogo가 이미 정했다. 자리만 옮긴다. 앵커와 피벗이 모두 한가운데라
            // 크기를 키워도 가로 가운데는 저절로 유지된다.
            logoRect.anchoredPosition = new Vector2(0f, LogoY);
        }
        else
        {
            Text title = PmdUi.MakeText(Root, "Title", "이상해씨의 던전 탐험", 84);
            title.color = PmdUi.AccentColor;
            Place(title.rectTransform, new Vector2(0f, 250f), new Vector2(900f, 110f));
        }

        commands.Clear();
        if (Flow.HasPlayedBefore && Flow.LastCharacter != null) commands.Add(Command.Continue);
        commands.Add(Command.Start);
        commands.Add(Command.Controls);
        commands.Add(Command.Credits);

        float y = MenuTop;
        for (int i = 0; i < commands.Count; i++)
        {
            entries.Add(PmdUi.MakeEntry(Root, "Menu" + i, LabelOf(commands[i]), MenuFontSize,
                new Vector2(0f, y), MenuSize));
            y -= MenuSize.y + MenuGap;
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
            case Command.Continue:
                // 지난 판의 캐릭터를 그대로 데려간다. 캐릭터 이름을 붙여 두었더니 칸이
                // 길어지고 조사("로"/"으로")까지 따라붙었다 — 무엇을 이어 하는지는 눌러 보면
                // 곧 나오므로 짧은 편이 낫다.
                return "이어하기";
            case Command.Start: return "시작하기";
            case Command.Controls: return "조작 방법";
            case Command.Credits: return "크레딧";
        }
        return "";
    }

    protected override void Activate(int index)
    {
        switch (commands[index])
        {
            case Command.Continue:
                Flow.ChooseCharacter(Flow.LastCharacter);
                break;
            case Command.Start:
                Flow.GoCharacterSelect();
                break;
            case Command.Controls:
                // 캐릭터를 고르기 전에도 조작은 볼 수 있어야 한다. 보고 나면 타이틀로 돌아온다.
                Flow.GoGuide(fromTitle: true);
                break;
            case Command.Credits:
                Tell("크레딧은 docs/CREDITS.md에 정리해 두었다.");
                break;
        }
    }

    private void Tell(string message)
    {
        if (notice != null) notice.text = message;
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
