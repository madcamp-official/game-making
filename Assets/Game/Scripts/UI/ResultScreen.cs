using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판이 끝난 화면. 쓰러졌을 때와 클리어했을 때 같은 화면을 쓰고 제목과 얼굴만 바꾼다.
///
/// 보여 주는 기록은 <see cref="RunStats"/>가 판 내내 모아 둔 것이다 — 플레이 시간, 처치 수,
/// 획득 골드 총합, 도달한 층·스테이지, 그리고 얻은 유물.
///
/// <b>얼굴은 쓰러진 그 모습의 표정 초상이다.</b> 이상해꽃으로 쓰러졌으면 이상해꽃의 Dizzy가,
/// 이상해꽃으로 던전을 깼으면 이상해꽃의 Happy가 뜬다. 어느 단계였는지는
/// <see cref="RunStats.StageIndex"/>가 들고 있다 — 화면이 세워질 때 플레이어가 아직
/// 살아 있으리라는 보장이 없어서 기록 쪽에 남겨 둔다.
///
/// 다음 갈 곳은 셋이다: 같은 캐릭터로 다시, 캐릭터 다시 고르기, 타이틀로.
/// </summary>
public class ResultScreen : FlowScreen
{
    private const int SortingOrder = 620;

    private bool cleared;

    public static ResultScreen Open(GameFlow flow, bool cleared)
    {
        var screen = Create<ResultScreen>(flow, "ResultScreen", SortingOrder);
        screen.cleared = cleared;
        screen.Fill();
        return screen;
    }

    // ---------------------------------------------------------------- 자리 값
    //
    // 얼굴과 기록을 <b>한 덩어리로 묶어</b> 창 가운데에 앉힌다. 예전에는 얼굴을 왼쪽 끝(−250)에,
    // 기록을 오른쪽(+80)에 각각 못 박아 두어서 둘 사이가 벌어지고 덩어리 전체가 왼쪽으로
    // 치우쳐 보였다. 아래 값들은 왼쪽부터 폭을 더해 나가며 잡는다.

    private const float PanelWidth = 900f;
    private const float PanelHeight = 500f;
    private const float PanelY = 60f;

    private const float FaceSize = 160f;      // 초상 40px × 4배 (정수배라야 픽셀이 안 깨진다)

    /// <summary>
    /// 초상을 두르는 남색 테두리의 두께.
    ///
    /// 좌하단 HP·EXP 꼬리표와 <b>같은 스프라이트</b>(<see cref="PmdUi.ChipBoldSprite"/>)를
    /// 쓰므로 두께가 저절로 같다 — 그 그림은 10×10에 9슬라이스 테두리가 4다. 이 값을 그
    /// 테두리와 다르게 두면 가운데가 늘어나는 자리가 어긋나 모서리가 뭉개진다.
    /// </summary>
    private const float FaceBorder = 4f;
    private const float FrameSize = FaceSize + FaceBorder * 2f;

    private const float FaceGap = 28f;

    /// <summary>
    /// 이름 칸의 폭. 값이 이름에서 얼마나 떨어져 시작하는지를 이 값 하나가 정한다 —
    /// 값 칸은 이름 칸 바로 오른쪽에 붙기 때문이다(<see cref="ValueLeft"/>).
    /// </summary>
    private const float LabelWidth = 240f;
    private const float ValueWidth = 380f;

    private const float GroupWidth = FrameSize + FaceGap + LabelWidth + ValueWidth;
    private const float GroupLeft = -GroupWidth * 0.5f;

    private const float FaceCenterX = GroupLeft + FrameSize * 0.5f;
    private const float LabelLeft = GroupLeft + FrameSize + FaceGap;
    private const float ValueLeft = LabelLeft + LabelWidth;

    /// <summary>기록 글자 크기. PMD 비트맵 폰트라 12의 배수만 쓸 수 있다.</summary>
    private const int StatFontSize = 36;
    private const float RowHeight = 46f;

    /// <summary>기록 덩어리와 얼굴이 함께 앉는 높이 (창 안 좌표).</summary>
    private const float GroupCenterY = -20f;

    /// <summary>
    /// 유물 이름을 몇 개까지 늘어놓을지. 넘으면 "외 N개"로 접는다.
    ///
    /// 글자를 키우면서 생긴 제약이다. 값 칸 폭에 한 줄 열 자 남짓 들어가는데 유물은 스무 개가
    /// 넘게 있어서, 다 적으면 창 밖으로 흘러 아래 버튼을 덮는다.
    /// </summary>
    private const int MaxRelicNames = 4;

    private Text heading;
    private Image portrait;
    private Image portraitFrame;
    private RectTransform panelRect;

    /// <summary>세운 줄들. 다 만든 뒤 한 번에 내리려고 들고 있는다 (<see cref="CenterRows"/>).</summary>
    private readonly List<RectTransform> rowRects = new List<RectTransform>();

    protected override void Build()
    {
        PmdUi.MakeBackdrop(Root, "Backdrop", 0.93f);

        Image panel = PmdUi.MakePanel(Root, "Panel");
        panelRect = panel.rectTransform;
        Place(panelRect, new Vector2(0f, PanelY), new Vector2(PanelWidth, PanelHeight));

        heading = PmdUi.MakeText(panelRect, "Heading", "", 44);
        Place(heading.rectTransform, new Vector2(0f, 190f), new Vector2(760f, 60f));

        // 초상을 남색 테두리로 두른다. 테두리와 속을 한 장이 겸한다 — 이 그림은 흰 속에 어두운
        // 윤곽이 구워져 있어서, 남색으로 물들이면 속은 남색 판이 되고 윤곽은 그 어두운 남색이
        // 된다(<see cref="PmdUi.MakeChip"/>에 같은 설명이 있다). 초상 뒤가 비어 있어도
        // 그 자리가 대화창과 같은 남색으로 차 보인다.
        // 보일지 말지는 Fill이 얼굴을 정한 뒤에 GameObject째로 정한다.
        portraitFrame = PmdUi.MakeSliced(panelRect, "PortraitFrame", PmdUi.ChipBoldSprite);
        portraitFrame.color = PmdUi.PanelFill;
        Place(portraitFrame.rectTransform, new Vector2(FaceCenterX, GroupCenterY),
              new Vector2(FrameSize, FrameSize));

        // 초상은 테두리의 자식이라 함께 움직인다. 테두리 두께만큼 안으로 물러선다.
        var portraitGo = new GameObject("Portrait", typeof(RectTransform));
        portraitGo.transform.SetParent(portraitFrame.rectTransform, false);
        portrait = portraitGo.AddComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        PmdUi.Stretch(portrait.rectTransform);
        portrait.rectTransform.offsetMin = new Vector2(FaceBorder, FaceBorder);
        portrait.rectTransform.offsetMax = new Vector2(-FaceBorder, -FaceBorder);

        entries.Add(PmdUi.MakeEntry(Root, "Retry", "같은 포켓몬으로 다시", 26,
            new Vector2(0f, -230f), new Vector2(420f, 52f)));
        entries.Add(PmdUi.MakeEntry(Root, "Reselect", "포켓몬 다시 고르기", 26,
            new Vector2(0f, -292f), new Vector2(420f, 52f)));
        entries.Add(PmdUi.MakeEntry(Root, "Title", "타이틀로 돌아가기", 26,
            new Vector2(0f, -354f), new Vector2(420f, 52f)));
        cursor = 0;
    }

    private void Fill()
    {
        heading.text = cleared ? "게임 클리어!" : "기절했다...";
        heading.color = cleared ? PmdUi.HighlightColor : new Color(0.95f, 0.5f, 0.5f);

        CharacterData character = RunStats.Character;

        // 얼굴이 없으면 테두리째 감춘다. 초상이 테두리의 자식이라 Image만 꺼서는 빈 남색
        // 사각형이 남는다 — 그림이 없는 자리에 틀만 서 있는 것이 가장 어색하다.
        Sprite face = FaceFor(character, RunStats.StageIndex, cleared);
        portrait.sprite = face;
        portraitFrame.gameObject.SetActive(face != null);

        // 항목을 한 줄씩 따로 세운다. 이름 칸과 값 칸을 <b>서로 다른 글자 상자</b>로 두어야
        // 값이 같은 열에서 시작한다 — 공백으로 밀어 맞추면 PMD 폰트에서 한글 한 칸이 공백
        // 셋 폭이라 "포켓몬"과 "플레이 시간"의 값이 어긋난다.
        int row = 0;
        if (character != null) AddRow(row++, "포켓몬", character.displayName);
        AddRow(row++, "플레이 시간", RunStats.ElapsedText);
        AddRow(row++, "도달", (RunStats.DeepestFloor + 1) + "층 " + (RunStats.DeepestRoom + 1) + " 스테이지");
        AddRow(row++, "처치", RunStats.Kills + "마리");
        AddRow(row++, "획득 골드", RunStats.GoldEarned + "G");
        AddRow(row++, "유물", RelicSummary());

        CenterRows(row);
    }

    /// <summary>
    /// 결과 화면에 세울 얼굴. 도달한 진화 단계의 표정 초상을 쓰고, 아직 그 초상을 넣지 않은
    /// 단계라면 예전처럼 정면 스프라이트로 되돌아간다 — 얼굴이 통째로 사라지는 것이 가장 나쁘다.
    /// </summary>
    private static Sprite FaceFor(CharacterData character, int stageIndex, bool cleared)
    {
        if (character == null) return null;

        PlayerEvolution.Stage[] stages = character.stages;
        if (stages == null || stages.Length == 0) return character.portrait;

        PlayerEvolution.Stage stage = stages[Mathf.Clamp(stageIndex, 0, stages.Length - 1)];
        if (stage == null) return character.portrait;

        Sprite face = cleared ? stage.happyPortrait : stage.dizzyPortrait;
        if (face != null) return face;
        return stage.portrait != null ? stage.portrait : character.portrait;
    }

    /// <summary>기록 한 줄. 이름은 왼쪽 칸에, 값은 그 오른쪽 칸에 각각 왼쪽 맞춤으로 놓는다.</summary>
    private void AddRow(int index, string label, string value)
    {
        float y = -index * RowHeight;

        Text name = PmdUi.MakeText(panelRect, "Label" + index, label, StatFontSize,
                                   TextAnchor.UpperLeft);
        PlaceLeft(name.rectTransform, LabelLeft, y, LabelWidth, RowHeight);
        name.color = PmdUi.DisabledColor;

        Text body = PmdUi.MakeText(panelRect, "Value" + index, value, StatFontSize,
                                   TextAnchor.UpperLeft);
        PlaceLeft(body.rectTransform, ValueLeft, y, ValueWidth, RowHeight);

        rowRects.Add(name.rectTransform);
        rowRects.Add(body.rectTransform);
    }

    /// <summary>
    /// 다 만든 줄 묶음을 얼굴과 같은 높이에 맞춰 통째로 내린다.
    ///
    /// 줄을 세울 때는 0을 기준으로 아래로 쌓아 두고, 몇 줄인지 알게 된 뒤에 여기서 한 번에
    /// 옮긴다 — 캐릭터가 없으면 "포켓몬" 줄이 빠져서 줄 수가 달라지기 때문이다.
    /// </summary>
    private void CenterRows(int rowCount)
    {
        float top = GroupCenterY + rowCount * RowHeight * 0.5f;

        foreach (RectTransform rt in rowRects)
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + top);
    }

    /// <summary>얻은 유물 이름을 죽 늘어놓는다. 너무 많으면 접고, 없으면 그렇다고 적는다.</summary>
    private static string RelicSummary()
    {
        RelicManager relics = RelicManager.Instance;
        if (relics == null || relics.Relics == null || relics.Relics.Count == 0) return "없음";

        int total = relics.Relics.Count;
        int shown = Mathf.Min(total, MaxRelicNames);

        var sb = new StringBuilder();
        for (int i = 0; i < shown; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(relics.Relics[i] != null ? relics.Relics[i].relicName : "?");
        }
        if (total > shown) sb.Append(" 외 ").Append(total - shown).Append("개");
        return sb.ToString();
    }

    protected override void Activate(int index)
    {
        switch (index)
        {
            case 0: Flow.RetrySameCharacter(); break;
            case 1: Flow.GoCharacterSelect(); break;
            default: Flow.GoTitle(); break;
        }
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    /// <summary>왼쪽 끝을 기준으로 놓는다. 값 칸이 같은 열에서 시작하려면 이쪽이라야 한다.</summary>
    private static void PlaceLeft(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(left, top);
    }
}
