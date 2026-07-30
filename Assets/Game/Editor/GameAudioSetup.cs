using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배경음을 층 데이터와 소리 목록에 이어 붙이는 일회성 도구.
///
/// mp3 열두 개를 어느 방에서 틀지 정하는 일은 결국 손으로 한 번 이어 주는 수밖에 없다.
/// 인스펙터에서 끌어다 놓는 대신 여기 적어 두는 이유는, <b>어느 곡이 어디에 걸렸는지</b>가
/// 한 화면에 보이기 때문이다. 층 에셋 셋을 번갈아 열어서는 3층 보스곡이 뭔지 알 수 없다.
///
/// 곁들여 임포트 설정도 맞춘다. 기본값(DecompressOnLoad)으로 두면 4MB짜리 mp3가 메모리에서
/// 30MB 넘는 PCM으로 풀린다 — 곡 열 개면 그것만으로 300MB다. 긴 곡은 스트리밍으로,
/// 짧은 효과음은 미리 풀어 두는 쪽으로 갈라 둔다.
///
/// 에디트 모드에서 <see cref="Run"/>을 한 번 실행할 것.
/// </summary>
public static class GameAudioSetup
{
    private const string BgmDir = "Assets/Game/Audio/BGM/";

    /// <summary>효과음 폴더. 지금은 진화음 둘만 여기 있고, 나머지 효과음은 BGM 폴더에 섞여 있다.</summary>
    private const string SfxDir = "Assets/Game/Audio/SFX/";
    private const string LibraryDir = "Assets/Game/Resources/Audio";
    private const string LibraryPath = LibraryDir + "/GameAudioLibrary.asset";
    private const string FloorDir = "Assets/Game/Data/Floors/";

    // 곡 이름은 파일명 그대로다. 원본 트랙 번호를 지우지 않는 편이 어느 사운드트랙에서 온
    // 곡인지 알아보기 쉬워서 그대로 둔다.
    private const string TopMenu = "03. Top Menu.mp3";
    private const string GameOver = "61. Game Over.mp3";
    private const string BeachAtDusk = "04. On the Beach at Dusk.mp3";
    private const string WigglytuffGuild = "08. Wigglytuff's Guild.mp3";
    private const string SpringCave = "116. Spring Cave.mp3";
    private const string SpacialCliffs = "129. Spacial Cliffs.mp3";
    private const string KecleonShop = "25. Kecleon's Shop.mp3";
    private const string AmpPlains = "36. Amp Plains.mp3";
    private const string NorthernDesert = "40. Northern Desert.mp3";
    private const string QuicksandCave = "41. Quicksand Cave.mp3";
    private const string ChasmCave = "48. Chasm Cave.mp3";
    private const string SealedRuinPit = "51. Sealed Ruin Pit.mp3";
    private const string GetItem = "getitem.mp3";
    private const string GetSkill = "getskill.mp3";
    private const string PlayerHit = "song409.mp3";
    private const string PlayerHurt = "song410.mp3";
    private const string UiHover = "hover.mp3";
    private const string UiClick = "click.mp3";
    private const string Evolving = "evolving.mp3";
    private const string Evolved = "evolved.mp3";
    private const string BossCry1 = "boss1.wav";
    private const string BossCry2 = "boss2.wav";
    private const string BossCry3 = "boss3.wav";

    /// <summary>
    /// 한 층에 걸리는 소리. 상점곡은 세 층이 같은 것을 쓴다 — 같은 상인의 같은 가게다.
    /// 울음소리는 층마다 다르다 (1층 버터플, 2층 코뿔몬, 3층 갸라도스).
    /// </summary>
    private readonly struct FloorTracks
    {
        public readonly string Asset, Battle, Event, Shop, Boss, Cry;

        public FloorTracks(string asset, string battle, string ev, string boss, string cry)
        {
            Asset = asset; Battle = battle; Event = ev; Shop = KecleonShop; Boss = boss; Cry = cry;
        }
    }

    private static readonly FloorTracks[] Floors =
    {
        new FloorTracks("Floor1_Forest.asset", AmpPlains, WigglytuffGuild, ChasmCave, BossCry1),
        new FloorTracks("Floor2_Desert.asset", QuicksandCave, NorthernDesert, SealedRuinPit, BossCry2),
        new FloorTracks("Floor3_Sea.asset", SpringCave, BeachAtDusk, SpacialCliffs, BossCry3),
    };

    public static void Run()
    {
        var log = new StringBuilder("[GameAudioSetup]\n");

        SetImportSettings(log);
        GameAudioLibrary library = BuildLibrary(log);
        AssignFloors(log);

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(log.ToString());
    }

    // ---------------------------------------------------------------- 임포트 설정

    private static void SetImportSettings(StringBuilder log)
    {
        var streaming = new List<string>
        {
            TopMenu, BeachAtDusk, WigglytuffGuild, SpringCave, SpacialCliffs, KecleonShop,
            AmpPlains, NorthernDesert, QuicksandCave, ChasmCave, SealedRuinPit,
        };

        foreach (string file in streaming) SetLoadType(BgmDir + file, AudioClipLoadType.Streaming, log);

        // 게임 오버 곡은 음악 자리에서 틀지만 2초짜리 스팅이다. 스트리밍으로 두면 쓰러진 순간
        // 스트림을 여느라 첫 음이 늦는데, 하필 그 자리가 가장 늦으면 안 되는 자리다.
        foreach (string file in new[] { GameOver, GetItem, GetSkill,
                                        PlayerHit, PlayerHurt, UiHover, UiClick,
                                        BossCry1, BossCry2, BossCry3 })
            SetLoadType(BgmDir + file, AudioClipLoadType.DecompressOnLoad, log);

        // 완료음(5초)은 확정되는 순간에 딱 맞춰 터져야 하므로 미리 풀어 둔다.
        SetLoadType(SfxDir + Evolved, AudioClipLoadType.DecompressOnLoad, log);

        // 진화음은 56초짜리 긴 곡이다. 풀어 두면 10MB 가까운 PCM이 되는데, 정작 컷씬은
        // 십몇 초 만에 끝나고 나머지는 잘려 나간다. 그렇다고 스트리밍으로 두면 컷씬이
        // 시작되는 순간 스트림을 여느라 첫 음이 늦는다 — 하필 "빛나기 시작했는데 조용한"
        // 한 박자가 생기는 자리다. 압축된 채로 메모리에 두면 둘 다 피한다.
        SetLoadType(SfxDir + Evolving, AudioClipLoadType.CompressedInMemory, log);
    }

    private static void SetLoadType(string path, AudioClipLoadType loadType, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) { log.AppendLine("  없음 " + path); return; }

        bool stream = loadType == AudioClipLoadType.Streaming;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = loadType;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.preloadAudioData = !stream;
        importer.defaultSampleSettings = settings;

        importer.loadInBackground = stream;
        importer.SaveAndReimport();

        log.AppendLine("  " + loadType + "  " + System.IO.Path.GetFileName(path));
    }

    // ---------------------------------------------------------------- 소리 목록

    private static GameAudioLibrary BuildLibrary(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(LibraryDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources"))
                AssetDatabase.CreateFolder("Assets/Game", "Resources");
            AssetDatabase.CreateFolder("Assets/Game/Resources", "Audio");
        }

        var library = AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<GameAudioLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            log.AppendLine("  만듦 " + LibraryPath);
        }

        library.menuBgm = Clip(TopMenu, log);
        library.gameOverBgm = Clip(GameOver, log);
        library.itemAcquired = Clip(GetItem, log);
        library.moveLearned = Clip(GetSkill, log);
        library.playerHit = Clip(PlayerHit, log);
        library.playerHurt = Clip(PlayerHurt, log);
        library.uiHover = Clip(UiHover, log);
        library.uiClick = Clip(UiClick, log);
        library.evolving = ClipIn(SfxDir, Evolving, log);
        library.evolved = ClipIn(SfxDir, Evolved, log);
        return library;
    }

    // ---------------------------------------------------------------- 층별 배정

    private static void AssignFloors(StringBuilder log)
    {
        foreach (FloorTracks tracks in Floors)
        {
            var floor = AssetDatabase.LoadAssetAtPath<FloorData>(FloorDir + tracks.Asset);
            if (floor == null) { log.AppendLine("  없음 " + FloorDir + tracks.Asset); continue; }

            floor.battleBgm = Clip(tracks.Battle, log);
            floor.eventBgm = Clip(tracks.Event, log);
            floor.shopBgm = Clip(tracks.Shop, log);
            floor.bossBgm = Clip(tracks.Boss, log);
            floor.bossCry = Clip(tracks.Cry, log);
            EditorUtility.SetDirty(floor);

            log.AppendLine("  " + tracks.Asset + "  전투=" + tracks.Battle + "  이벤트=" + tracks.Event +
                           "  상점=" + tracks.Shop + "  보스=" + tracks.Boss + "  울음=" + tracks.Cry);
        }
    }

    private static AudioClip Clip(string file, StringBuilder log) => ClipIn(BgmDir, file, log);

    private static AudioClip ClipIn(string dir, string file, StringBuilder log)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + file);
        if (clip == null) log.AppendLine("  못 찾음 " + dir + file);
        return clip;
    }
}
