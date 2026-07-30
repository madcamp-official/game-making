using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// PMDCollab 시트 → 8방향 스프라이트/클립/컨트롤러 파이프라인.
///
/// 지금까지는 세션마다 임시 코드로 처리하던 것을 파일로 굳혔다. 새 캐릭터를 들일 때:
/// 1. 시트를 <c>Assets/ThirdParty/PMDCollab/{덱스}_{이름}/Source/</c>에 AnimData.xml과 함께 둔다
/// 2. 쓸 시트만 <c>Assets/Game/Art/Characters/{이름}/Sprites/{동작}.png</c>로 복사한다
/// 3. <see cref="Import"/>를 호출한다 (아래 Specs에 항목 추가)
///
/// 규칙 (기존 캐릭터들과 동일):
/// * 슬라이스 이름 <c>{동작}_{행}_{열}</c>. 행은 시트 위에서부터 0=남, 1=남동, … 7=남서
/// * PPU 32, 점 필터, 무압축
/// * 클립 <c>{동작}_{행}</c>, 프레임 시간은 AnimData의 Duration ÷ 60
/// * Idle 시트가 없으면 Walk 첫 프레임으로 Idle 클립을 합성한다
/// * 한 번만 재생하는 동작(loop=false)은 끝 프레임에서 멈춘다 — 방어 자세 유지 등에 쓴다
///
/// ⚠️ <c>TextureImporter.spritesheet</c>는 Unity 6에서 조용히 무시된다.
/// 반드시 <see cref="ISpriteEditorDataProvider"/>로 슬라이스해야 한다.
///
/// ⚠️ 다시 실행하면 컨트롤러를 지우고 새로 만들어 GUID가 바뀐다.
/// 그 컨트롤러를 참조하는 프리팹도 반드시 다시 구워야 한다 (<see cref="Floor2EnemySetup.BuildPrefabs"/>).
/// </summary>
public static class PmdCharacterPipeline
{
    public class AnimSpec
    {
        public string target;   // 게임 쪽 동작 이름 (상태·클립·스프라이트 이름에 쓰인다)
        public string source;   // AnimData.xml 안의 원본 이름 (Special0 → Roll처럼 다를 수 있다)
        public bool loop;

        public AnimSpec(string target, string source, bool loop)
        {
            this.target = target;
            this.source = source;
            this.loop = loop;
        }
    }

    /// <summary>
    /// 다른 시트의 한 프레임만 떼어 만드는 정지 클립. 자세를 유지해야 하는 상태에 쓴다.
    ///
    /// 클립을 끝 프레임에 멈춰 두는 방법도 있지만, 끝 프레임이 원하는 자세라는 보장이 없다 —
    /// 고지의 Attack은 "말기 → 구르기 → 펴기"라 마지막이 <b>일어선</b> 그림이다. 방어 자세는
    /// 가운데의 공 모양 프레임이므로, 쓸 프레임을 이름으로 못박는 편이 읽기도 고치기도 쉽다.
    /// </summary>
    public class FrameClipSpec
    {
        public string name;    // 만들 클립·상태 이름
        public string source;  // 가져올 시트 (게임 쪽 동작 이름)
        public int frame;      // 그 시트의 몇 번째 열

        public FrameClipSpec(string name, string source, int frame)
        {
            this.name = name;
            this.source = source;
            this.frame = frame;
        }
    }

    /// <summary>
    /// 다른 시트의 프레임 구간만 잘라 만드는 한 번짜리 클립.
    /// 정지 자세(FrameClipSpec)에서 원래 자세로 <b>돌아가는</b> 뒷부분만 필요할 때 쓴다 —
    /// 고지가 공 모양에서 몸을 펴는 Uncurl이 Attack의 7~10 프레임이다.
    /// </summary>
    public class RangeClipSpec
    {
        public string name;    // 만들 클립·상태 이름
        public string source;  // 가져올 시트 (게임 쪽 동작 이름)
        public int from, to;   // 프레임 구간 (양 끝 포함)

        public RangeClipSpec(string name, string source, int from, int to)
        {
            this.name = name;
            this.source = source;
            this.from = from;
            this.to = to;
        }
    }

    public class CharacterSpec
    {
        public string name;        // Characters/ 아래 폴더 이름
        public string thirdParty;  // ThirdParty/PMDCollab/ 아래 폴더 이름
        public AnimSpec[] anims;
        public FrameClipSpec[] frameClips = new FrameClipSpec[0];
        public RangeClipSpec[] rangeClips = new RangeClipSpec[0];

        public CharacterSpec(string name, string thirdParty, params AnimSpec[] anims)
        {
            this.name = name;
            this.thirdParty = thirdParty;
            this.anims = anims;
        }
    }

    /// <summary>2층 적 다섯 종.</summary>
    public static readonly CharacterSpec[] Floor2Enemies =
    {
        new CharacterSpec("Sandslash", "0028_Sandslash",
            new AnimSpec("Walk", "Walk", true),
            // 후려치기. 정면 할퀴기 한 번.
            new AnimSpec("Strike", "Strike", false),
            // 몸을 말아 굴러가는 동작. 물러날 때 쓴다 (말기 0~2 · 구르기 3~9 · 펴기 10).
            new AnimSpec("Attack", "Attack", false))
        {
            // 방어 자세는 구르기가 가장 멀리 나아가 잠깐 멈춰 보이는 구간(5~8, 오프셋 +19.6px로
            // 동일)의 공 모양이다. Attack의 마지막 프레임은 몸을 다시 편 그림이라 쓰면 안 된다.
            frameClips = new[] { new FrameClipSpec("Guard", "Attack", 6) },
            // 방어가 풀리면 남은 프레임으로 몸을 편다.
            rangeClips = new[] { new RangeClipSpec("Uncurl", "Attack", 7, 10) },
        },
        new CharacterSpec("Marowak", "0105_Marowak",
            new AnimSpec("Walk", "Walk", true),
            // 뼈다귀가 돌아올 때까지 던진 자세로 기다린다.
            new AnimSpec("Shoot", "Shoot", false)),
        new CharacterSpec("Dugtrio", "0051_Dugtrio",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Walk", "Walk", true)),
        new CharacterSpec("Ninetales", "0038_Ninetales",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Shoot", "Shoot", false)),
        new CharacterSpec("Graveler", "0075_Graveler",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Charge", "Charge", true),
            // 명세의 "special19"는 저장소에 없다. 웅크려 구르는 Special0을 Roll이라는 이름으로 쓴다.
            new AnimSpec("Roll", "Special0", true)),
    };

    /// <summary>2층 이벤트의 NPC 스승 둘. 아래(남쪽)를 보고 수련 동작을 반복한다.</summary>
    public static readonly CharacterSpec[] EventNpcs =
    {
        new CharacterSpec("Hitmonlee", "0106_Hitmonlee",
            new AnimSpec("Idle", "Idle", true),
            // 수련 중 — 계속 반복한다.
            new AnimSpec("Kick", "Kick", true)),
        new CharacterSpec("Hitmonchan", "0107_Hitmonchan",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Punch", "Punch", true)),
    };

    /// <summary>
    /// 3층 일반 적 다섯. CC 연계가 컨셉이라 공격 동작이 저마다 다른 시트를 쓴다.
    /// 킹크랩 가위(Strike)만 한 번짜리고, 나머지(흡인·냉기·레이저·해류)는 지속 동작이라 반복이다.
    /// </summary>
    public static readonly CharacterSpec[] Floor3Enemies =
    {
        new CharacterSpec("Kingler", "0099_Kingler",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Strike", "Strike", false)),
        new CharacterSpec("Poliwrath", "0062_Poliwrath",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Charge", "Charge", true),   // 흡인
            new AnimSpec("Idle", "Idle", true)),      // 충격파도 이 동작으로 낸다
        new CharacterSpec("Dewgong", "0087_Dewgong",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Charge", "Charge", true)),  // 냉기 분사
        new CharacterSpec("Starmie", "0121_Starmie",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Idle", "Idle", true)),      // 레이저 중 배속 회전
        new CharacterSpec("Dragonair", "0148_Dragonair",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Charge", "Charge", true)),  // 해류 전환
    };

    /// <summary>
    /// 3층 이벤트의 라프라스. 계곡 왼쪽에서 쉼 없이 Idle을 반복하고, 태워 줄 때는
    /// 같은 Idle인 채 방향(행)만 동쪽으로 바꾼다 — 헤엄 전용 시트가 따로 없다.
    /// </summary>
    public static readonly CharacterSpec[] Floor3EventNpcs =
    {
        new CharacterSpec("Lapras", "0131_Lapras",
            new AnimSpec("Idle", "Idle", true)),
    };

    /// <summary>
    /// 3층 보스가 불러내는 소환물.
    ///
    /// 잉어킹은 갸라도스가 세워 두는 <b>고정 장애물</b>이라 걸어 다니지 않는다. 제자리에서
    /// 파닥이는 Hop 하나면 충분하다.
    /// </summary>
    public static readonly CharacterSpec[] Floor3Summons =
    {
        new CharacterSpec("Magikarp", "0129_Magikarp",
            new AnimSpec("Hop", "Hop", true)),
    };

    /// <summary>
    /// 플레이어 캐릭터 두 계열 (파이리·꼬부기 진화 계열). 동작 이름은 기술과 이렇게 맺는다:
    /// Shoot=불꽃세례·물대포, Strike=드래곤클로, Charge=화염방사·하이드로펌프, Ricochet=로켓박치기,
    /// Walk=파도타기 돌진. 용의춤은 전용 동작이 없다 (상태 변화 기술).
    ///
    /// * 파이리는 저장소에 Shoot이 Charge의 CopyOf로만 있어 <b>Charge 시트를 Shoot으로</b> 들인다.
    /// * 꼬부기는 Ricochet이 없지만 로켓박치기는 어니부기부터 배우므로 필요하지 않다.
    /// * Charge·Ricochet은 이어지는 기술 동안 돌아야 해서 반복이고, Shoot·Strike는 한 번이다.
    /// </summary>
    public static readonly CharacterSpec[] PlayerLines =
    {
        new CharacterSpec("Charmander", "0004_Charmander",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Shoot", "Charge", false),
            new AnimSpec("Strike", "Strike", false),
            new AnimSpec("Charge", "Charge", true)),
        new CharacterSpec("Charmeleon", "0005_Charmeleon",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Shoot", "Shoot", false),
            new AnimSpec("Strike", "Strike", false),
            new AnimSpec("Charge", "Charge", true)),
        new CharacterSpec("Charizard", "0006_Charizard",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Shoot", "Shoot", false),
            new AnimSpec("Strike", "Strike", false),
            new AnimSpec("Charge", "Charge", true)),
        new CharacterSpec("Squirtle", "0007_Squirtle",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Shoot", "Shoot", false),
            new AnimSpec("Charge", "Charge", true)),
        new CharacterSpec("Wartortle", "0008_Wartortle",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Shoot", "Shoot", false),
            new AnimSpec("Ricochet", "Ricochet", true),
            new AnimSpec("Charge", "Charge", true)),
        new CharacterSpec("Blastoise", "0009_Blastoise",
            new AnimSpec("Idle", "Idle", true),
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("Shoot", "Shoot", false),
            new AnimSpec("Ricochet", "Ricochet", true),
            new AnimSpec("Charge", "Charge", true)),
    };

    /// <summary>플레이어 두 계열만 굽는다. 나누는 이유는 <see cref="ImportFloor3Summons"/>와 같다.</summary>
    public static string ImportPlayerLines()
    {
        var log = new System.Text.StringBuilder();
        foreach (CharacterSpec spec in PlayerLines)
            log.AppendLine(Import(spec));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private const int PixelsPerUnit = 32;

    public static string ImportAll()
    {
        var log = new System.Text.StringBuilder();
        foreach (CharacterSpec spec in Floor2Enemies)
            log.AppendLine(Import(spec));
        foreach (CharacterSpec spec in EventNpcs)
            log.AppendLine(Import(spec));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>
    /// 소환물만 다시 굽는다. <see cref="ImportAll"/>과 나눠 둔 이유는 컨트롤러를 새로 만들면
    /// GUID가 바뀌기 때문이다 — 필요 없는 캐릭터까지 함께 구우면 그 프리팹들도 전부 다시 구워야 한다.
    /// </summary>
    public static string ImportFloor3Summons()
    {
        var log = new System.Text.StringBuilder();
        foreach (CharacterSpec spec in Floor3Summons)
            log.AppendLine(Import(spec));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>3층 일반 적만 굽는다. 나누는 이유는 <see cref="ImportFloor3Summons"/>와 같다.</summary>
    public static string ImportFloor3Enemies()
    {
        var log = new System.Text.StringBuilder();
        foreach (CharacterSpec spec in Floor3Enemies)
            log.AppendLine(Import(spec));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>3층 이벤트 NPC(라프라스)만 굽는다. 나누는 이유는 <see cref="ImportFloor3Summons"/>와 같다.</summary>
    public static string ImportFloor3EventNpcs()
    {
        var log = new System.Text.StringBuilder();
        foreach (CharacterSpec spec in Floor3EventNpcs)
            log.AppendLine(Import(spec));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>
    /// 2층 개편(사막의 맹공)에서 추가된 성원숭. 이동은 Walk, 공격은 MultiStrike 2연타.
    /// 새 종이라 참조하는 곳이 없어 <see cref="Import"/>를 그대로 써도 안전하다.
    /// </summary>
    public static string ImportPrimeape()
    {
        return Import(new CharacterSpec("Primeape", "0057_Primeape",
            new AnimSpec("Walk", "Walk", true),
            new AnimSpec("MultiStrike", "MultiStrike", false))
        {
            // 주먹을 들어 올린 채 멈춘 프레임 — 연타 직전의 준비 자세로 쓴다.
            frameClips = new[] { new FrameClipSpec("Ready", "MultiStrike", 2) },
        });
    }

    /// <summary>
    /// 이미 구워 둔 캐릭터의 시트에서 프레임 하나를 골라 정지 자세 상태로 덧붙인다
    /// (고지의 StrikeReady — 발톱을 들어 올린 예고 자세). <see cref="AddAnim"/>과 같은
    /// 이유로 다시 슬라이스하지 않는다 — 이미 잘린 스프라이트를 그대로 집는다.
    /// </summary>
    public static string AddHoldAnim(string species, string clipName, string sourceSheet, int frame)
    {
        string charRoot = "Assets/Game/Art/Characters/" + species;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            charRoot + "/" + species + ".controller");
        if (controller == null) return species + ": 컨트롤러가 없다 — Import를 먼저";

        var byName = AssetDatabase.LoadAllAssetsAtPath(charRoot + "/Sprites/" + sourceSheet + ".png")
            .OfType<Sprite>().ToDictionary(s => s.name);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        for (int row = 0; row < 8; row++)
        {
            if (!byName.TryGetValue(sourceSheet + "_" + row + "_" + frame, out Sprite sprite))
                return species + ": " + sourceSheet + "_" + row + "_" + frame + " 스프라이트 없음";
            AnimationClip clip = MakeHoldClip(charRoot, clipName + "_" + row, sprite);
            AnimatorState existing = machine.states
                .Select(s => s.state).FirstOrDefault(s => s.name == clip.name);
            if (existing != null)
            {
                existing.motion = clip;
                continue;
            }
            AnimatorState state = machine.AddState(clip.name);
            state.motion = clip;
            state.writeDefaultValues = true;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return species + ": " + clipName + " 정지 자세 8방향 (" + sourceSheet + " " + frame + "번 프레임)";
    }

    public static string Import(CharacterSpec spec)
    {
        string charRoot = "Assets/Game/Art/Characters/" + spec.name;
        // 빈 폴더는 리프레시에서 사라질 수 있다. 클립을 만들기 전에 반드시 보장한다.
        if (!AssetDatabase.IsValidFolder(charRoot + "/Animations"))
            AssetDatabase.CreateFolder(charRoot, "Animations");

        string animDataPath = "Assets/ThirdParty/PMDCollab/" + spec.thirdParty + "/Source/AnimData.xml";
        var animData = LoadAnimData(animDataPath);

        // Idle 시트가 없으면 Walk 첫 프레임으로 합성한다.
        bool hasRealIdle = spec.anims.Any(a => a.target == "Idle");

        var clips = new List<AnimationClip>();
        // 정지·구간 클립이 다른 시트의 프레임을 집어야 해서, 시트별 스프라이트와 시간표를 들고 있는다.
        var spritesByAnim = new Dictionary<string, Dictionary<string, Sprite>>();
        var entriesByAnim = new Dictionary<string, AnimEntry>();

        foreach (AnimSpec anim in spec.anims)
        {
            if (!animData.TryGetValue(anim.source, out AnimEntry entry))
                return spec.name + ": AnimData에 " + anim.source + " 없음";

            string sheetPath = charRoot + "/Sprites/" + anim.target + ".png";
            Slice(sheetPath, anim.target, entry);

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToArray();
            var byName = sprites.ToDictionary(s => s.name);
            spritesByAnim[anim.target] = byName;
            entriesByAnim[anim.target] = entry;

            for (int row = 0; row < 8; row++)
            {
                clips.Add(MakeClip(charRoot, anim.target, row, entry, byName, anim.loop));
                if (!hasRealIdle && anim.target == "Walk")
                    clips.Add(MakeHoldClip(charRoot, "Idle_" + row, byName["Walk_" + row + "_0"]));
            }
        }

        foreach (FrameClipSpec frameClip in spec.frameClips)
        {
            if (!spritesByAnim.TryGetValue(frameClip.source, out var byName))
                return spec.name + ": " + frameClip.source + " 시트를 먼저 넣어야 한다";
            for (int row = 0; row < 8; row++)
                clips.Add(MakeHoldClip(charRoot, frameClip.name + "_" + row,
                    byName[frameClip.source + "_" + row + "_" + frameClip.frame]));
        }

        foreach (RangeClipSpec rangeClip in spec.rangeClips)
        {
            if (!spritesByAnim.TryGetValue(rangeClip.source, out var byName))
                return spec.name + ": " + rangeClip.source + " 시트를 먼저 넣어야 한다";
            AnimEntry entry = entriesByAnim[rangeClip.source];
            for (int row = 0; row < 8; row++)
                clips.Add(MakeRangeClip(charRoot, rangeClip, row, entry, byName));
        }

        BuildController(charRoot + "/" + spec.name + ".controller", clips);
        return spec.name + ": 시트 " + spec.anims.Length + "개, 클립 " + clips.Count + "개";
    }

    /// <summary>
    /// 이미 구워 둔 캐릭터에 동작 하나만 더 얹는다.
    ///
    /// <see cref="Import"/>는 컨트롤러를 지우고 새로 만들기 때문에 GUID가 바뀌고, 그 캐릭터의
    /// 스프라이트도 전부 다시 잘라 파일 ID가 바뀐다 — 프리팹의 기본 스프라이트, 애니메이션
    /// 클립, <c>PmdFootShadow</c>가 맺어 둔 본체·그림자 짝이 한꺼번에 끊어진다. 동작 하나를
    /// 추가하려고 그 값을 치를 이유가 없다.
    ///
    /// 그래서 여기서는 <b>새 시트만</b> 자르고(참조하는 곳이 아직 없으니 안전하다), 클립을
    /// 만들어, 있는 컨트롤러에 상태만 덧붙인다. 같은 이름의 상태가 이미 있으면 모션만 갈아
    /// 끼우므로 다시 실행해도 상태가 늘어나지 않는다.
    /// </summary>
    public static string AddAnim(string species, string thirdParty, AnimSpec anim)
    {
        string charRoot = "Assets/Game/Art/Characters/" + species;
        string controllerPath = charRoot + "/" + species + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) return species + ": 컨트롤러가 없다 — Import를 먼저";

        var animData = LoadAnimData("Assets/ThirdParty/PMDCollab/" + thirdParty + "/Source/AnimData.xml");
        if (!animData.TryGetValue(anim.source, out AnimEntry entry))
            return species + ": AnimData에 " + anim.source + " 없음";

        string sheetPath = charRoot + "/Sprites/" + anim.target + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath) == null)
            return species + ": " + sheetPath + "가 없다";
        Slice(sheetPath, anim.target, entry);

        var byName = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                                  .ToDictionary(s => s.name);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        for (int row = 0; row < 8; row++)
        {
            AnimationClip clip = MakeClip(charRoot, anim.target, row, entry, byName, anim.loop);
            AnimatorState existing = machine.states
                .Select(s => s.state).FirstOrDefault(s => s.name == clip.name);
            if (existing != null)
            {
                existing.motion = clip;
                continue;
            }
            AnimatorState state = machine.AddState(clip.name);
            state.motion = clip;
            state.writeDefaultValues = true;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return species + ": " + anim.target + " 8방향 추가 (타격 " + entry.HitTime.ToString("0.00") + "초)";
    }

    // ---------------------------------------------------------------- AnimData

    public class AnimEntry
    {
        public int frameWidth, frameHeight;
        public int[] durations;   // 60fps 틱 단위
        public int hitFrame = -1; // 투사체가 나가는 프레임 (없으면 -1)

        /// <summary>동작 시작부터 타격 프레임이 끝나는 순간까지의 시간(초).</summary>
        public float HitTime
        {
            get
            {
                if (hitFrame < 0) return 0f;
                int ticks = 0;
                for (int i = 0; i <= hitFrame && i < durations.Length; i++) ticks += durations[i];
                return ticks / 60f;
            }
        }
    }

    public static Dictionary<string, AnimEntry> LoadAnimData(string path)
    {
        var doc = new XmlDocument();
        doc.Load(Path.GetFullPath(path));
        var result = new Dictionary<string, AnimEntry>();
        foreach (XmlNode anim in doc.SelectNodes("//Anim"))
        {
            string name = anim.SelectSingleNode("Name")?.InnerText;
            if (name == null || anim.SelectSingleNode("CopyOf") != null) continue;
            var durations = anim.SelectNodes("Durations/Duration").Cast<XmlNode>()
                .Select(n => int.Parse(n.InnerText)).ToArray();
            result[name] = new AnimEntry
            {
                frameWidth = int.Parse(anim.SelectSingleNode("FrameWidth").InnerText),
                frameHeight = int.Parse(anim.SelectSingleNode("FrameHeight").InnerText),
                durations = durations,
                hitFrame = anim.SelectSingleNode("HitFrame") != null
                    ? int.Parse(anim.SelectSingleNode("HitFrame").InnerText) : -1,
            };
        }
        return result;
    }

    // ---------------------------------------------------------------- 슬라이스

    private static void Slice(string sheetPath, string animName, AnimEntry entry)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
        int columns = tex.width / entry.frameWidth;

        var rects = new List<SpriteRect>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                rects.Add(new SpriteRect
                {
                    name = animName + "_" + row + "_" + col,
                    spriteID = GUID.Generate(),
                    rect = new Rect(col * entry.frameWidth,
                                    tex.height - (row + 1) * entry.frameHeight,
                                    entry.frameWidth, entry.frameHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });
            }
        }

        provider.SetSpriteRects(rects.ToArray());
        var nameFileId = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameFileId.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- 클립

    private static AnimationClip MakeClip(string charRoot, string animName, int row,
                                          AnimEntry entry, Dictionary<string, Sprite> byName, bool loop)
    {
        var keys = new List<ObjectReferenceKeyframe>();
        float time = 0f;
        Sprite last = null;
        for (int i = 0; i < entry.durations.Length; i++)
        {
            if (!byName.TryGetValue(animName + "_" + row + "_" + i, out Sprite sprite)) continue;
            keys.Add(new ObjectReferenceKeyframe { time = time, value = sprite });
            time += entry.durations[i] / 60f;
            last = sprite;
        }
        // 마지막 프레임의 길이를 지키기 위해 끝 시각에 같은 스프라이트를 한 번 더 찍는다.
        if (last != null) keys.Add(new ObjectReferenceKeyframe { time = time, value = last });

        return SaveClip(charRoot, animName + "_" + row, keys, loop);
    }

    /// <summary>시트의 프레임 구간만 잘라 만든 한 번짜리 클립. 시간표는 AnimData 그대로다.</summary>
    private static AnimationClip MakeRangeClip(string charRoot, RangeClipSpec range, int row,
                                               AnimEntry entry, Dictionary<string, Sprite> byName)
    {
        var keys = new List<ObjectReferenceKeyframe>();
        float time = 0f;
        Sprite last = null;
        for (int i = range.from; i <= range.to && i < entry.durations.Length; i++)
        {
            Sprite sprite = byName[range.source + "_" + row + "_" + i];
            keys.Add(new ObjectReferenceKeyframe { time = time, value = sprite });
            time += entry.durations[i] / 60f;
            last = sprite;
        }
        if (last != null) keys.Add(new ObjectReferenceKeyframe { time = time, value = last });
        return SaveClip(charRoot, range.name + "_" + row, keys, false);
    }

    /// <summary>한 장으로 된 정지 클립. 자세를 그대로 유지한다.</summary>
    private static AnimationClip MakeHoldClip(string charRoot, string clipName, Sprite sprite)
    {
        var keys = new List<ObjectReferenceKeyframe>
        {
            new ObjectReferenceKeyframe { time = 0f, value = sprite },
            new ObjectReferenceKeyframe { time = 1f / 60f, value = sprite },
        };
        return SaveClip(charRoot, clipName, keys, true);
    }

    private static AnimationClip SaveClip(string charRoot, string clipName,
                                          List<ObjectReferenceKeyframe> keys, bool loop)
    {
        var clip = new AnimationClip { frameRate = 60, name = clipName };
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite",
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = charRoot + "/Animations/" + clipName + ".anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // ---------------------------------------------------------------- 컨트롤러

    private static void BuildController(string path, List<AnimationClip> clips)
    {
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState defaultState = null;
        foreach (AnimationClip clip in clips)
        {
            AnimatorState state = machine.AddState(clip.name);
            state.motion = clip;
            state.writeDefaultValues = true;
            if (clip.name == "Idle_0") defaultState = state;
        }
        if (defaultState != null) machine.defaultState = defaultState;
    }
}
