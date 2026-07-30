using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 흐름: 타이틀 → 캐릭터 선택 → 조작 안내 → 게임 → 결과.
///
/// 화면 하나가 다음 화면을 직접 열지 않고 모두 여기를 거친다. 그래야 "타이틀로 돌아가기"나
/// "캐릭터 다시 고르기"처럼 <b>여러 화면에서 같은 곳으로 가는 길</b>을 한 군데서 정할 수 있다.
/// 화면끼리 서로를 알면 길이 늘어날 때마다 모든 화면을 고쳐야 한다.
///
/// 타이틀과 캐릭터 선택을 나눈 것은 의도한 구조다. 설정·크레딧·도감 같은 메뉴가 늘어날 자리가
/// 타이틀에 생기고, 캐릭터를 고르는 일 자체가 판의 첫 선택처럼 무게를 갖는다.
///
/// 게임이 도는 동안에도 이 오브젝트는 살아 있다. 상태를 들고 있는 유일한 곳이라, 죽거나
/// 클리어했을 때 결과 화면을 띄우는 것도 여기서 한다.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum State { Title, CharacterSelect, Guide, Playing, Result }

    public static GameFlow Instance { get; private set; }

    /// <summary>고를 수 있는 캐릭터. 에디터에서 채운다 (CharacterData 에셋 목록).</summary>
    [SerializeField] private CharacterData[] characters;

    /// <summary>지금 화면.</summary>
    public State Current { get; private set; } = State.Title;

    /// <summary>이번 판에 고른 캐릭터.</summary>
    public CharacterData Selected { get; private set; }

    public IReadOnlyList<CharacterData> Characters => characters;

    /// <summary>마지막으로 고른 캐릭터의 이름. 타이틀의 '빠른 시작'이 이걸 본다.</summary>
    private const string LastCharacterKey = "lastCharacter";

    /// <summary>조작 안내를 다시 보지 않기로 했는지.</summary>
    private const string SkipGuideKey = "skipControlsGuide";

    /// <summary>한 번이라도 플레이했는지. 타이틀에 '빠른 시작'을 띄울지 정한다.</summary>
    public bool HasPlayedBefore => !string.IsNullOrEmpty(PlayerPrefs.GetString(LastCharacterKey, ""));

    public bool SkipGuide
    {
        get => PlayerPrefs.GetInt(SkipGuideKey, 0) != 0;
        set { PlayerPrefs.SetInt(SkipGuideKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>지난 판에 쓴 캐릭터. 없거나 목록에서 사라졌으면 null.</summary>
    public CharacterData LastCharacter
    {
        get
        {
            string name = PlayerPrefs.GetString(LastCharacterKey, "");
            if (string.IsNullOrEmpty(name) || characters == null) return null;
            foreach (CharacterData c in characters)
                if (c != null && c.name == name) return c;
            return null;
        }
    }

    private TitleScreen title;
    private CharacterSelectScreen select;
    private ControlsGuideScreen guide;
    private ResultScreen result;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 게임은 아직 시작하지 않는다. 방을 미리 올려 두면 타이틀 뒤에서 적이 움직이고
        // 시간이 흐른다 — 판이 언제 시작됐는지가 흐려진다.
        GoTitle();
    }

    // ---------------------------------------------------------------- 화면 전환

    public void GoTitle()
    {
        CloseAll();
        Current = State.Title;
        Time.timeScale = 0f;
        title = TitleScreen.Open(this);
    }

    public void GoCharacterSelect()
    {
        CloseAll();
        Current = State.CharacterSelect;
        Time.timeScale = 0f;
        select = CharacterSelectScreen.Open(this);
    }

    /// <summary>캐릭터를 골랐다. 안내를 건너뛰기로 했다면 바로 시작한다.</summary>
    public void ChooseCharacter(CharacterData character)
    {
        if (character == null) return;
        Selected = character;
        PlayerPrefs.SetString(LastCharacterKey, character.name);
        PlayerPrefs.Save();

        if (SkipGuide) BeginRun();
        else GoGuide();
    }

    public void GoGuide()
    {
        CloseAll();
        Current = State.Guide;
        Time.timeScale = 0f;
        guide = ControlsGuideScreen.Open(this, Selected);
    }

    /// <summary>고른 캐릭터로 판을 시작한다.</summary>
    public void BeginRun()
    {
        if (Selected == null && characters != null && characters.Length > 0) Selected = characters[0];
        CloseAll();
        Current = State.Playing;

        RunStats.Begin(Selected);
        ApplyCharacter(Selected);

        Time.timeScale = 1f;
        if (RoomFlowController.Instance != null) RoomFlowController.Instance.BeginRun();
    }

    /// <summary>죽었거나 클리어했다. 결과 화면을 띄운다.</summary>
    public void FinishRun(bool cleared)
    {
        if (Current == State.Result) return;
        Current = State.Result;
        RunStats.Finish();
        CloseAll();
        Time.timeScale = 0f;
        result = ResultScreen.Open(this, cleared);
    }

    /// <summary>같은 캐릭터로 다시. 판만 새로 깔면 되므로 고른 것을 그대로 쓴다.</summary>
    public void RetrySameCharacter() => BeginRun();

    // ---------------------------------------------------------------- 캐릭터 입히기

    /// <summary>
    /// 고른 캐릭터를 플레이어에게 입힌다. 진화 단계 배열을 통째로 갈아 끼우면
    /// 그림·체력·공격력·진화 뒤 모습까지 한꺼번에 따라온다.
    /// </summary>
    private void ApplyCharacter(CharacterData character)
    {
        if (character == null || character.stages == null || character.stages.Length == 0) return;

        var evolution = FindAnyObjectByType<PlayerEvolution>();
        if (evolution == null) return;
        evolution.LoadStages(character.stages);
    }

    private void CloseAll()
    {
        if (title != null) { title.Close(); title = null; }
        if (select != null) { select.Close(); select = null; }
        if (guide != null) { guide.Close(); guide = null; }
        if (result != null) { result.Close(); result = null; }
    }
}
