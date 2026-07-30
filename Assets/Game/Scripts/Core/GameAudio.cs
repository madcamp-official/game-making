using System.Collections;
using UnityEngine;

/// <summary>
/// 게임의 소리를 트는 유일한 창구. 음악 한 줄기와 효과음을 맡는다.
///
/// 씬에 올려 두지 않고 판이 열리기 전에 스스로 태어난다. 소리를 내고 싶은 쪽(방을 까는 곳,
/// 유물을 주는 곳)이 저마다 AudioSource를 들고 있으면 <b>지금 무슨 곡이 흐르는지</b> 아는 곳이
/// 없어진다 — 방을 넘길 때마다 같은 곡이 처음부터 다시 시작하는 것도 그래서 생긴다.
///
/// 그래서 여기서 딱 두 가지를 지킨다.
/// <list type="bullet">
/// <item>같은 곡을 다시 틀라고 하면 <b>아무 일도 하지 않는다</b>. 1층 전투방 넷을 지나는 동안
/// Amp Plains는 끊기지 않고 이어진다.</item>
/// <item>곡이 바뀔 때는 두 재생기를 엇갈려 겹쳐 넘긴다. 뚝 끊고 새로 시작하면 방문을 지날 때마다
/// 소리에 이음매가 생긴다.</item>
/// </list>
///
/// 메뉴는 <c>Time.timeScale = 0</c>으로 시간을 멈춘 채 도므로, 겹쳐 넘기는 계산은 전부
/// 실제 시간(unscaled)으로 센다. 스케일 시간으로 세면 타이틀에서 음악이 영영 페이드되지 않는다.
/// </summary>
public class GameAudio : MonoBehaviour
{
    private const string LibraryResourcePath = "Audio/GameAudioLibrary";

    public static GameAudio Instance { get; private set; }

    private GameAudioLibrary library;

    /// <summary>음악 재생기 둘. 겹쳐 넘기려면 새 곡이 들어올 자리가 하나 비어 있어야 한다.</summary>
    private AudioSource[] music;
    private int active;

    private AudioSource sfx;
    private Coroutine fadeRoutine;

    /// <summary>음악 크기에 곱하는 값. 1이 제 크기, 싸움이 끝난 방에서는 이보다 낮아진다.</summary>
    private float duck = 1f;
    private float duckTarget = 1f;

    /// <summary>지금 흐르는 곡. 같은 곡을 다시 틀라는 요청을 걸러내는 기준이다.</summary>
    public AudioClip CurrentBgm { get; private set; }

    /// <summary>
    /// 첫 씬이 열리기 전에 스스로 선다.
    ///
    /// 씬에 오브젝트로 심어 두지 않는 이유는, 이 게임이 씬 하나로 돌아가고 그 씬을 통째로 다시
    /// 올리는 길(게임 오버 뒤 R)이 남아 있기 때문이다. 씬에 심으면 그때마다 새로 태어나 음악이
    /// 처음부터 다시 시작한다. 여기서 만들고 <see cref="Object.DontDestroyOnLoad"/>로 남긴다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        go.AddComponent<GameAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        library = Resources.Load<GameAudioLibrary>(LibraryResourcePath);
        if (library == null)
            Debug.LogWarning("GameAudio: Resources/" + LibraryResourcePath + " 을 찾지 못했다. 메뉴 음악과 효과음이 나오지 않는다.");

        music = new[] { CreateSource("Music A", true), CreateSource("Music B", true) };
        sfx = CreateSource("Sfx", false);
        // PlayOneShot의 크기는 재생기 volume에 곱해진다. 음악 재생기와 달리 여기는 1로 열어 두고,
        // 실제 크기는 PlayOneShot 인자로 준다.
        sfx.volume = 1f;
    }

    private AudioSource CreateSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        // 소리가 카메라와의 거리에 따라 작아지면 안 된다. mp3 임포터의 3D 기본값을 여기서 덮는다.
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    /// <summary>
    /// 받아 줄 귀가 있는지 본다.
    ///
    /// AudioListener가 없으면 재생기는 멀쩡히 돌고 <c>isPlaying</c>도 true인데 소리만 나지 않는다.
    /// 아무 데서도 티가 나지 않아서, 실제로 이 프로젝트의 Main Camera에서 리스너가 빠져 있는 것을
    /// 한참 뒤에야 알았다. 같은 일이 다시 조용히 지나가지 않도록 한 번 짚고 넘어간다.
    ///
    /// <see cref="Awake"/>가 아니라 여기서 보는 이유는 순서다. 이 오브젝트는 첫 씬이 열리기 전에
    /// 서므로, Awake 시점에는 카메라가 아직 없어서 무조건 없다고 나온다.
    /// </summary>
    private void Start()
    {
        if (FindAnyObjectByType<AudioListener>() == null)
            Debug.LogWarning("GameAudio: 씬에 AudioListener가 없다. 음악과 효과음이 재생은 되지만 들리지 않는다 — Main Camera에 붙일 것.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- 음악

    /// <summary>메뉴 음악(타이틀·캐릭터 선택·조작 안내)으로 갈아탄다.</summary>
    public static void PlayMenuBgm()
    {
        if (Instance == null || Instance.library == null) return;
        Instance.SwitchTo(Instance.library.menuBgm);
    }

    /// <summary>
    /// 쓰러졌다. 듣던 곡을 걷어 내고 게임 오버 곡을 <b>한 번만</b> 흘린다.
    ///
    /// 다른 곡과 달리 되풀이하지 않는다. 곡이 끝나면 그대로 조용해지고, 다음 화면으로 넘어가야
    /// (타이틀로 나가거나 다시 시작하거나) 다시 소리가 난다. 판이 끝났다는 것을 침묵으로
    /// 남겨 두는 자리라, 곡을 물려 두면 그 여운이 사라진다.
    /// </summary>
    public static void PlayGameOverBgm()
    {
        if (Instance == null || Instance.library == null) return;
        Instance.SwitchTo(Instance.library.gameOverBgm, loop: false);
    }

    /// <summary>
    /// 음악을 <paramref name="clip"/>으로 갈아탄다. 이미 그 곡이 흐르고 있으면 그대로 둔다.
    /// null을 주면 서서히 재운다.
    /// </summary>
    public static void PlayBgm(AudioClip clip)
    {
        if (Instance != null) Instance.SwitchTo(clip);
    }

    /// <summary>음악을 끈다 (겹쳐 넘기는 시간만큼 서서히).</summary>
    public static void StopBgm() => PlayBgm(null);

    /// <summary>
    /// 음악을 줄인다. 1이면 제 크기, 0.3이면 세 할만.
    ///
    /// 곡을 바꾸지 않고 크기만 건드리는 길을 따로 둔 이유는, 싸움이 끝나도 <b>같은 곡이 계속
    /// 흘러야</b> 하기 때문이다. 방이 조용해졌다는 것을 곡을 갈아 끼워 알리면 빈 방을 정리하는
    /// 30초 동안 방금 듣던 곡이 끊긴다.
    ///
    /// 내려갈 때든 올라갈 때든 <see cref="GameAudioLibrary.duckFade"/>에 적힌 속도로 굴러간다 —
    /// 그 자리에서 바꾸는 길은 두지 않았다. 크기가 계단처럼 튀면 그 순간이 소리의 흠집으로
    /// 들린다. 곡 자체가 바뀌는 자리는 <see cref="SwitchTo"/>가 따로 맡는다.
    /// </summary>
    public static void SetBgmDuck(float scale)
    {
        if (Instance != null) Instance.duckTarget = Mathf.Clamp01(scale);
    }

    /// <summary>싸움이 끝났다. 음악을 라이브러리에 적어 둔 만큼 낮춘다.</summary>
    public static void DuckForClearedRoom()
    {
        if (Instance != null && Instance.library != null)
            SetBgmDuck(Instance.library.clearedRoomVolumeScale);
    }

    /// <summary>지금 음악 재생기가 향해야 할 크기. 기본 크기에 감쇠를 곱한 값이다.</summary>
    private float TargetVolume => (library != null ? library.bgmVolume : 0.5f) * duck;

    /// <summary>
    /// 감쇠를 목표치로 굴리고, 겹쳐 넘기는 중이 아니면 재생기 크기를 맞춰 둔다.
    ///
    /// 크기를 코루틴 한 곳에서만 정하지 않고 여기서도 손보는 이유는, 감쇠가 곡 전환과 상관없이
    /// 아무 때나 들어오기 때문이다. 겹쳐 넘기는 동안에는 <see cref="Crossfade"/>가 주인이고
    /// (그쪽도 <see cref="TargetVolume"/>을 매 프레임 다시 읽으므로 감쇠가 같이 먹는다),
    /// 끝난 뒤에는 여기가 주인이다.
    /// </summary>
    private void Update()
    {
        if (music == null) return;

        if (!Mathf.Approximately(duck, duckTarget))
        {
            float speed = library != null && library.duckFade > 0f ? 1f / library.duckFade : 4f;
            duck = Mathf.MoveTowards(duck, duckTarget, speed * Time.unscaledDeltaTime);
        }

        if (fadeRoutine == null) music[active].volume = CurrentBgm != null ? TargetVolume : 0f;
    }

    private void SwitchTo(AudioClip clip, bool loop = true)
    {
        if (music == null) return;
        // 방을 넘겨도 같은 곡이면 건드리지 않는다. 이 한 줄이 층 안에서 음악이 이어지게 한다.
        if (clip == CurrentBgm && (clip == null || music[active].isPlaying)) return;

        CurrentBgm = clip;

        // 곡이 바뀐다는 것은 장면이 바뀐다는 뜻이다. 지난 방에서 낮춰 둔 감쇠를 여기서 턴다 —
        // 정리한 방에서 곧바로 쓰러지면 게임 오버 곡이 세 할 크기로 시작할 뻔했다.
        duck = duckTarget = 1f;

        AudioSource from = music[active];
        active = 1 - active;
        AudioSource to = music[active];

        float duration = library != null ? library.crossfade : 0.5f;

        to.Stop();
        to.clip = clip;
        to.loop = loop;
        to.volume = 0f;
        if (clip != null) to.Play();

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(from, to, clip != null, duration));
    }

    /// <summary>
    /// 목표 크기를 미리 계산해 두지 않고 매 프레임 <see cref="TargetVolume"/>을 다시 읽는다.
    /// 곡을 넘기는 도중에 감쇠가 들어와도(마지막 적이 방문 앞에서 죽는 경우) 따라간다.
    /// </summary>
    private IEnumerator Crossfade(AudioSource from, AudioSource to, bool toAudible, float duration)
    {
        float fromStart = from.volume;

        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                to.volume = Mathf.Lerp(0f, toAudible ? TargetVolume : 0f, k);
                yield return null;
            }
        }

        from.volume = 0f;
        from.Stop();
        from.clip = null;
        to.volume = toAudible ? TargetVolume : 0f;
        fadeRoutine = null;
    }

    // ---------------------------------------------------------------- 효과음

    /// <summary>무언가를 손에 넣었다 — 유물이든 상점에서 산 회복약이든.</summary>
    public static void PlayItemAcquired()
    {
        if (Instance != null && Instance.library != null) Instance.PlaySfx(Instance.library.itemAcquired, 1f);
    }

    /// <summary>기술 강화를 골랐다.</summary>
    public static void PlayMoveLearned()
    {
        if (Instance != null && Instance.library != null) Instance.PlaySfx(Instance.library.moveLearned, 1f);
    }

    /// <summary>주인공의 공격이 적에게 닿았다.</summary>
    public static void PlayPlayerHit()
    {
        if (Instance != null && Instance.library != null)
            Instance.PlaySfx(Instance.library.playerHit, Instance.library.hitVolumeScale);
    }

    /// <summary>주인공이 얻어맞았다.</summary>
    public static void PlayPlayerHurt()
    {
        if (Instance != null && Instance.library != null)
            Instance.PlaySfx(Instance.library.playerHurt, Instance.library.hitVolumeScale);
    }

    /// <summary>
    /// 메뉴에서 가리키는 칸이 바뀌었다. "바뀐 순간"을 가려내는 일은 부르는 쪽이 한다
    /// (<see cref="PmdUi.TrackHoverSound"/>) — 매 프레임 부르면 커서를 올려 둔 내내 이어진다.
    /// </summary>
    public static void PlayUiHover()
    {
        if (Instance != null && Instance.library != null)
            Instance.PlaySfx(Instance.library.uiHover, Instance.library.uiVolumeScale);
    }

    /// <summary>
    /// 효과음 하나를 낸다. 어느 소리를 낼지 <b>부르는 쪽이 들고 있는</b> 경우를 위한 문이다 —
    /// 보스 울음소리처럼 층마다 다른 소리는 라이브러리에 이름으로 박아 둘 수 없다.
    /// </summary>
    public static void PlaySfx(AudioClip clip)
    {
        if (Instance != null) Instance.PlaySfx(clip, 1f);
    }

    private void PlaySfx(AudioClip clip, float scale)
    {
        if (clip == null || sfx == null) return;
        sfx.PlayOneShot(clip, (library != null ? library.sfxVolume : 0.9f) * scale);
    }
}
