using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판이 끝난 화면. 죽었을 때와 클리어했을 때 같은 화면을 쓰고 제목과 색만 바꾼다.
///
/// 보여 주는 기록은 <see cref="RunStats"/>가 판 내내 모아 둔 것이다 — 플레이 시간, 처치 수,
/// 획득 골드 총합, 도달한 층·방, 그리고 얻은 유물. 클리어 화면에서는 고른 캐릭터의
/// <b>최종 진화 모습</b>을 함께 세운다.
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

    private Text heading;
    private Text stats;
    private Image portrait;

    protected override void Build()
    {
        PmdUi.MakeBackdrop(Root, "Backdrop", 0.93f);

        Image panel = PmdUi.MakePanel(Root, "Panel");
        Place(panel.rectTransform, new Vector2(0f, 50f), new Vector2(780f, 460f));

        heading = PmdUi.MakeText(panel.rectTransform, "Heading", "", 44);
        Place(heading.rectTransform, new Vector2(0f, 180f), new Vector2(720f, 60f));

        var portraitGo = new GameObject("Portrait", typeof(RectTransform));
        portraitGo.transform.SetParent(panel.rectTransform, false);
        portrait = portraitGo.AddComponent<Image>();
        portrait.preserveAspect = true;
        portrait.enabled = false;
        Place(portrait.rectTransform, new Vector2(-250f, 10f), new Vector2(150f, 150f));

        stats = PmdUi.MakeText(panel.rectTransform, "Stats", "", 26, TextAnchor.UpperLeft);
        Place(stats.rectTransform, new Vector2(80f, 0f), new Vector2(460f, 250f));

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
        heading.text = cleared ? "던전을 정복했다!" : "쓰러졌다...";
        heading.color = cleared ? PmdUi.HighlightColor : new Color(0.95f, 0.5f, 0.5f);

        CharacterData character = RunStats.Character;
        if (character != null)
        {
            // 클리어했으면 마지막 진화 모습, 죽었으면 고른 그대로의 모습.
            Sprite face = character.portrait;
            if (cleared && character.stages != null && character.stages.Length > 0)
            {
                Sprite last = character.stages[character.stages.Length - 1].portrait;
                if (last != null) face = last;
            }
            portrait.sprite = face;
            portrait.enabled = face != null;
        }

        var sb = new StringBuilder();
        if (character != null) sb.AppendLine("포켓몬       " + character.displayName);
        sb.AppendLine("플레이 시간  " + RunStats.ElapsedText);
        sb.AppendLine("도달         " + (RunStats.DeepestFloor + 1) + "층 "
                      + (RunStats.DeepestRoom + 1) + "번방");
        sb.AppendLine("처치         " + RunStats.Kills + "마리");
        sb.AppendLine("획득 골드    " + RunStats.GoldEarned + "G");
        sb.AppendLine("유물         " + RelicSummary());
        stats.text = sb.ToString();
    }

    /// <summary>얻은 유물 이름을 죽 늘어놓는다. 없으면 그렇다고 적는다.</summary>
    private static string RelicSummary()
    {
        RelicManager relics = RelicManager.Instance;
        if (relics == null || relics.Relics == null || relics.Relics.Count == 0) return "없음";

        var sb = new StringBuilder();
        for (int i = 0; i < relics.Relics.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(relics.Relics[i] != null ? relics.Relics[i].relicName : "?");
        }
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
}
