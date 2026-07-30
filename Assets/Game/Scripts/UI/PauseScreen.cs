using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 일시정지 메뉴. 판 도중에 Esc로 열고, Esc나 "계속하기"로 판에 돌아간다.
///
/// 배경은 <b>반투명</b>으로 깐다(<see cref="PmdUi.MakeBackdrop"/>). 타이틀·조작 안내처럼
/// 불투명하게 덮으면 판이 끝난 것처럼 보인다 — 뒤에 멈춘 전투가 비쳐야 "잠깐 세워 둔 것"으로
/// 읽힌다. 곡도 같은 이유로 갈지 않는다.
///
/// 시간을 세우고 되돌리는 일은 <see cref="GameFlow"/>가 맡는다. 이 화면은 버튼 세 개를 그리고
/// 눌린 것을 알릴 뿐이다 — timeScale을 만지는 자리가 흩어지면 누가 세웠는지 좇을 수 없게 된다.
/// </summary>
public class PauseScreen : FlowScreen
{
    /// <summary>흐름 화면들(600~620)보다 위. 일시정지는 판의 HUD까지 덮어야 한다.</summary>
    private const int SortingOrder = 630;

    private const int ButtonFontSize = 26;
    private static readonly Vector2 ButtonSize = new Vector2(420f, 52f);
    private const float ButtonTop = 30f;
    private const float ButtonGap = 62f;

    /// <summary>열린 프레임. 연 Esc가 같은 프레임의 <see cref="LateUpdate"/>에 다시 보인다.</summary>
    private int openedFrame;

    public static PauseScreen Open(GameFlow flow) =>
        Create<PauseScreen>(flow, "PauseScreen", SortingOrder);

    protected override void Build()
    {
        openedFrame = Time.frameCount;

        PmdUi.MakeBackdrop(Root, "Backdrop");

        Text heading = PmdUi.MakeText(Root, "Heading", "일시정지", 48);
        heading.color = PmdUi.AccentColor;
        RectTransform headingRect = heading.rectTransform;
        headingRect.anchorMin = headingRect.anchorMax = new Vector2(0.5f, 0.5f);
        headingRect.pivot = new Vector2(0.5f, 0.5f);
        headingRect.sizeDelta = new Vector2(600f, 70f);
        headingRect.anchoredPosition = new Vector2(0f, 150f);

        // 위에서부터 계속하기 → 조작 방법 → 메인화면. 가장 자주 누를 것이 첫 칸이다.
        entries.Add(PmdUi.MakeEntry(Root, "Resume", "계속하기", ButtonFontSize,
            new Vector2(0f, ButtonTop), ButtonSize));
        entries.Add(PmdUi.MakeEntry(Root, "Controls", "조작 방법", ButtonFontSize,
            new Vector2(0f, ButtonTop - ButtonGap), ButtonSize));
        entries.Add(PmdUi.MakeEntry(Root, "Title", "메인화면으로 가기", ButtonFontSize,
            new Vector2(0f, ButtonTop - ButtonGap * 2f), ButtonSize));
        cursor = 0;
    }

    protected override void Activate(int index)
    {
        switch (index)
        {
            case 0: Flow.ResumeRun(); break;
            case 1: Flow.GoGuide(fromPause: true); break;
            case 2: Flow.GoTitle(); break;
        }
    }

    /// <summary>
    /// Esc — 연 키로 닫기도 한다. 뼈대(<see cref="FlowScreen"/>)의 Update가 이미 차 있어
    /// LateUpdate를 쓴다. 연 프레임은 거른다 — 연 Esc가 그대로 닫는 Esc로 읽히면
    /// 메뉴가 뜨자마자 사라진다.
    /// </summary>
    private void LateUpdate()
    {
        if (Time.frameCount == openedFrame) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Flow.ResumeRun();
    }
}
