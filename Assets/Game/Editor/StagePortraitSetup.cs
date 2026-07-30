using UnityEditor;
using UnityEngine;

/// <summary>
/// 진화 단계마다 <b>표정 초상</b>(결과 화면)과 <b>컷씬 그림</b>(진화 연출)을 연결하는 일회성 도구.
///
/// 세 계열 아홉 단계가 각각 세 장을 쓴다:
/// <list type="bullet">
/// <item><c>Art/Portraits/{종}_Dizzy.png</c> — 쓰러진 결과 화면</item>
/// <item><c>Art/Portraits/{종}_Happy.png</c> — 클리어 결과 화면</item>
/// <item><c>Art/Characters/Evolution/{종}.png</c> — 진화 컷씬</item>
/// </list>
///
/// 손으로 연결하면 스물일곱 칸이라 하나쯤 빠뜨리기 쉽고, 빠뜨린 자리는 게임을 끝까지
/// 가 봐야 드러난다. 이름 규칙이 뚜렷하니 코드가 집는 편이 낫다.
///
/// 다시 실행해도 안전하다 — 같은 파일을 같은 칸에 다시 넣을 뿐이다. 못 찾은 파일은
/// 그 칸을 <b>건드리지 않고</b> 로그로만 알린다. 빈 칸으로 덮어써서 이미 있던 연결을
/// 지우는 것이 가장 나쁘다.
/// </summary>
public static class StagePortraitSetup
{
    private const string CharacterDir = "Assets/Game/Data/Characters/";
    private const string PortraitDir = "Assets/Game/Art/Portraits/";
    private const string EvolutionArtDir = "Assets/Game/Art/Characters/Evolution/";

    /// <summary>캐릭터 에셋 이름과, 그 계열의 진화 단계별 영문 종 이름 (배열 순서 = 단계 순서).</summary>
    private static readonly (string asset, string[] species)[] Lines =
    {
        ("Bulbasaur",  new[] { "Bulbasaur",  "Ivysaur",    "Venusaur"  }),
        ("Charmander", new[] { "Charmander", "Charmeleon", "Charizard" }),
        ("Squirtle",   new[] { "Squirtle",   "Wartortle",  "Blastoise" }),
    };

    [MenuItem("Game/진화 단계 초상·컷씬 그림 연결")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new System.Text.StringBuilder();
        int wired = 0, missing = 0;

        FixEvolutionArtImport(log);

        foreach ((string asset, string[] species) line in Lines)
        {
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterDir + line.asset + ".asset");
            if (data == null) { log.AppendLine(line.asset + ".asset이 없다"); missing++; continue; }

            if (data.stages == null || data.stages.Length == 0)
            {
                log.AppendLine(line.asset + ": 진화 단계가 비어 있다");
                missing++;
                continue;
            }

            int count = Mathf.Min(data.stages.Length, line.species.Length);
            if (data.stages.Length != line.species.Length)
                log.AppendLine(line.asset + ": 단계 " + data.stages.Length +
                               "개인데 종 이름은 " + line.species.Length + "개다 — 겹치는 " +
                               count + "단계만 연결한다");

            for (int i = 0; i < count; i++)
            {
                PlayerEvolution.Stage stage = data.stages[i];
                if (stage == null) { log.AppendLine(line.asset + " " + i + "단계가 비었다"); missing++; continue; }

                string species = line.species[i];

                if (TryLoad(PortraitDir + species + "_Dizzy.png", log, ref missing, out Sprite dizzy))
                    stage.dizzyPortrait = dizzy;
                if (TryLoad(PortraitDir + species + "_Happy.png", log, ref missing, out Sprite happy))
                    stage.happyPortrait = happy;
                // 컷씬 그림만 파일 이름이 소문자다 (Art/Characters/Evolution의 규칙).
                if (TryLoad(EvolutionArtDir + species.ToLowerInvariant() + ".png", log, ref missing, out Sprite art))
                    stage.evolutionArt = art;

                wired++;
            }

            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        log.Insert(0, "진화 단계 " + wired + "개 연결, 못 찾은 그림 " + missing + "개\n");
        return log.ToString();
    }

    /// <summary>
    /// 컷씬 그림의 임포트 설정을 맞춘다.
    ///
    /// 그림은 PokeAPI의 4세대 하트골드·소울실버 정면 스프라이트(80×80)다. 받아 온 그대로는
    /// Bilinear 필터라 4배로 키우면 획이 뭉개진다 — 점 필터에 정수배로 키워야 원본 픽셀이
    /// 그대로 보인다.
    ///
    /// ⚠️ <b>PPU는 100으로 둔다.</b> 이 프로젝트의 픽셀 아트 규칙은 32지만 그것은 월드에
    /// 놓이는 스프라이트가 타일 격자에 맞게 하려는 값이다. 이 그림들은 컷씬의 UI
    /// <c>Image</c>에만 쓰이고, 거기서 <c>SetNativeSize</c>가 <c>rect / (PPU / 캔버스 기준
    /// 100)</c>로 크기를 잡는다. 32로 두면 80픽셀짜리가 250으로 잡히고 거기에 4배가 더
    /// 곱해져 화면을 넘어간다.
    ///
    /// Single로 두는 것도 뜻이 있다. 잘라내기(Multiple)를 쓰면 종마다 여백이 다르게 깎여
    /// 단계별 크기 비교가 흐트러지는데, 통짜 80×80이면 원본이 그려 둔 <b>몸집 차이가
    /// 그대로</b> 남는다 — 이상해씨는 작고 이상해꽃은 크다.
    ///
    /// ⚠️ <b>메시는 FullRect여야 한다.</b> 기본값 Tight는 알파가 옅은 가장자리를 메시에서
    /// 빼 버린다 — 도트가 아닌 HGSS 스프라이트는 윤곽이 반투명으로 부드럽게 깎여 있어서,
    /// 그 가장자리가 통째로 잘려 나갔다. 리자몽처럼 프레임에 꽉 찬 종에서 특히 티가 났다.
    /// FullRect는 rect 전체를 사각형 두 장으로 덮으므로 한 픽셀도 잃지 않고, UI에서는
    /// 그리는 비용도 더 싸다.
    /// </summary>
    private static void FixEvolutionArtImport(System.Text.StringBuilder log)
    {
        int fixedCount = 0;
        foreach ((string asset, string[] species) line in Lines)
        {
            foreach (string name in line.species)
            {
                string path = EvolutionArtDir + name.ToLowerInvariant() + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { log.AppendLine("컷씬 그림 없음: " + path); continue; }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;

                // 메시 종류는 importer에 직접 난 창이 없어 설정 묶음을 통째로 읽고 되돌려 준다.
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(platform);

                importer.SaveAndReimport();
                fixedCount++;
            }
        }
        log.AppendLine("컷씬 그림 임포트 정리 " + fixedCount +
                       "장 (Point · 압축 없음 · PPU 100 · Single · FullRect)");
    }

    /// <summary>
    /// 스프라이트 한 장을 집는다. 없으면 로그만 남기고 false — 부르는 쪽이 그 칸을
    /// 건드리지 않게 하려는 것이다.
    /// </summary>
    private static bool TryLoad(string path, System.Text.StringBuilder log, ref int missing,
                                out Sprite sprite)
    {
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return true;
        log.AppendLine("못 찾음: " + path);
        missing++;
        return false;
    }
}
