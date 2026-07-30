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

    /// <summary>
    /// 이번 판에 실제로 입힌 완성 캐릭터. 미구현 캐릭터를 골랐으면 그 캐릭터가 지정한
    /// 폴백이 들어간다. <see cref="Selected"/>는 재도전과 마지막 선택 기억을 위해 그대로 둔다.
    /// </summary>
    public CharacterData ActiveCharacter { get; private set; }

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

    /// <summary>
    /// 메뉴 음악을 건다. 타이틀 → 캐릭터 선택 → 조작 안내는 한 흐름이라 곡이 이어져야 하는데,
    /// <see cref="GameAudio.PlayMenuBgm"/>이 같은 곡 요청을 걸러 주므로 화면마다 불러도 끊기지 않는다.
    /// 결과 화면은 <see cref="FinishRun"/>에서 갈린다 — 깼으면 메뉴 곡, 쓰러졌으면 게임 오버 곡.
    /// </summary>
    public void GoTitle()
    {
        CloseAll();
        Current = State.Title;
        Time.timeScale = 0f;
        GameAudio.PlayMenuBgm();
        title = TitleScreen.Open(this);
    }

    public void GoCharacterSelect()
    {
        CloseAll();
        Current = State.CharacterSelect;
        Time.timeScale = 0f;
        GameAudio.PlayMenuBgm();
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

    /// <summary>
    /// 조작 안내를 연다.
    /// </summary>
    /// <param name="fromTitle">
    /// 타이틀에서 구경하러 들어온 길인지. 그 길에는 시작할 캐릭터를 넘기지 않는다 —
    /// 안내 화면이 "다음에 무엇이 오는가"를 캐릭터의 유무로 판단하기 때문이다.
    ///
    /// <b>고른 캐릭터가 있느냐로 판단할 수는 없다.</b> 한 판 하고 타이틀로 돌아오면
    /// <see cref="Selected"/>가 지난 판의 것으로 남아 있어서, 구경하러 들어온 것인데도
    /// "시작"이 떠 버린다.
    /// </param>
    public void GoGuide(bool fromTitle = false)
    {
        CloseAll();
        Current = State.Guide;
        Time.timeScale = 0f;
        GameAudio.PlayMenuBgm();
        guide = ControlsGuideScreen.Open(this, fromTitle ? null : Selected);
    }

    /// <summary>고른 캐릭터로 판을 시작한다.</summary>
    public void BeginRun()
    {
        if (Selected == null && characters != null && characters.Length > 0) Selected = characters[0];
        ActiveCharacter = ResolvePlayableCharacter(Selected);
        if (ActiveCharacter == null)
        {
            Debug.LogError("플레이 가능한 CharacterData가 없다. 진화 단계·기술 세트 또는 fallbackCharacter를 확인한다.");
            GoCharacterSelect();
            return;
        }

        CloseAll();
        Current = State.Playing;

        RunStats.Begin(ActiveCharacter);
        ResetRunState();
        ApplyCharacter(ActiveCharacter);
        // 캐릭터의 1단계를 입힌 뒤에 되살려야 그 단계의 최대 체력으로 찬다.
        ResetPlayer();

        Time.timeScale = 1f;
        if (RoomFlowController.Instance != null) RoomFlowController.Instance.BeginRun();
    }

    /// <summary>
    /// 지난 판에서 쌓인 것을 전부 비운다.
    ///
    /// 예전에는 판을 다시 시작하는 길이 <b>씬을 통째로 다시 올리는 것</b>뿐이라 (게임 오버 뒤 R키)
    /// 아무것도 비울 필요가 없었다. 결과 화면에서 이어서 시작하게 되면서 씬이 그대로 남고,
    /// 골드·유물·레벨·배운 기술이 전부 다음 판으로 넘어간다. 판에 걸쳐 남아야 하는 것은
    /// 마지막으로 고른 캐릭터(PlayerPrefs)뿐이다.
    ///
    /// 여기에 모아 두는 이유는 "판이 시작된다"를 아는 곳이 여기 하나이기 때문이다. 각자
    /// 알아서 비우게 하면 무엇이 언제 비워지는지가 흩어진다.
    /// </summary>
    private void ResetRunState()
    {
        if (RunManager.Instance != null) RunManager.Instance.ResetForNewRun();
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.ResetForNewRun();
        if (PlayerMoves.Instance != null) PlayerMoves.Instance.ResetForNewRun();
        EventBuffs.ResetForNewRun();
        // 유물을 마지막에 비운다. 이때 도는 OnRelicsChanged가 최대 체력·이동 속도 배율을
        // 다시 계산해 플레이어에게 밀어 넣는다 — 그 뒤에 캐릭터를 입히고 되살려야 한다.
        if (RelicManager.Instance != null) RelicManager.Instance.ResetForNewRun();
    }

    /// <summary>쓰러진 몸을 일으킨다. 캐릭터를 입힌 뒤에 불러야 한다.</summary>
    private static void ResetPlayer()
    {
        var death = FindAnyObjectByType<PlayerDeathHandler>();
        // 못 찾으면 조용히 넘어가지 않고 남긴다. 이걸 건너뛴 판은 체력 0에 조작이 꺼진 몸으로
        // 시작하는데, 그 증상만 보고 여기까지 거슬러 오기는 어렵다.
        if (death == null) { Debug.LogWarning("PlayerDeathHandler를 찾지 못해 몸을 일으키지 못했다."); return; }
        death.ResetForNewRun();
    }

    /// <summary>죽었거나 클리어했다. 결과 화면을 띄운다.</summary>
    public void FinishRun(bool cleared)
    {
        if (Current == State.Result) return;
        Current = State.Result;
        RunStats.Finish();
        CloseAll();
        Time.timeScale = 0f;
        // 쓰러진 것과 깬 것은 같은 화면이지만 같은 소리일 수 없다. 게임 오버 곡은 한 번만
        // 흐르고 조용해지므로, 다음 화면으로 넘어갈 때까지 결과 화면은 침묵 속에 남는다.
        if (cleared) GameAudio.PlayMenuBgm();
        else GameAudio.PlayGameOverBgm();
        result = ResultScreen.Open(this, cleared);
    }

    /// <summary>같은 캐릭터로 다시. 판만 새로 깔면 되므로 고른 것을 그대로 쓴다.</summary>
    public void RetrySameCharacter() => BeginRun();

    // ---------------------------------------------------------------- 캐릭터 입히기

    /// <summary>
    /// 고른 캐릭터를 플레이어에게 입힌다. 진화 단계와 기술 세트를 함께 갈아 끼우면
    /// 그림·체력·공격력·진화 뒤 모습·습득 순서가 한꺼번에 따라온다.
    /// </summary>
    private void ApplyCharacter(CharacterData character)
    {
        if (character == null || !character.HasOwnImplementation) return;

        var moves = FindAnyObjectByType<PlayerMoves>();
        if (moves != null) moves.LoadMoveSet(character.moveSet);
        else Debug.LogWarning("PlayerMoves를 찾지 못해 캐릭터 기술 세트를 적용하지 못했다.");

        // 단계별 위력을 어느 기술에 넣을지 알려면 기술 세트가 먼저 들어가 있어야 한다.
        var evolution = FindAnyObjectByType<PlayerEvolution>();
        if (evolution != null) evolution.LoadStages(character.stages);
        else Debug.LogWarning("PlayerEvolution을 찾지 못해 캐릭터 진화 단계를 적용하지 못했다.");
    }

    /// <summary>
    /// 선택 데이터의 폴백을 따라 실제 구현을 찾는다. 연결이 빠졌을 때도 첫 번째 완성 캐릭터로
    /// 안전하게 시작하지만, 미구현 캐릭터에는 명시적인 fallbackCharacter를 연결하는 것이 원칙이다.
    /// </summary>
    public CharacterData ResolvePlayableCharacter(CharacterData requested)
    {
        CharacterData resolved = requested != null ? requested.ResolvePlayable() : null;
        if (resolved != null) return resolved;

        if (characters == null) return null;
        foreach (CharacterData candidate in characters)
        {
            resolved = candidate != null ? candidate.ResolvePlayable() : null;
            if (resolved != null) return resolved;
        }
        return null;
    }

    private void CloseAll()
    {
        if (title != null) { title.Close(); title = null; }
        if (select != null) { select.Close(); select = null; }
        if (guide != null) { guide.Close(); guide = null; }
        if (result != null) { result.Close(); result = null; }
    }
}
