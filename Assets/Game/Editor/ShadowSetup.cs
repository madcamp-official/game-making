using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 모든 캐릭터(주인공·이벤트 NPC·적·보스)에 SpriteCollab 발밑 그림자를 다는 일회성 도구.
///
/// 1단계 <see cref="SliceAll"/>: 각 종의 {애니}Shadow.png(흰색 마스크 — 크기 등급을
/// 반영해 미리 구워 둔 것)를 본체 시트와 같은 격자로 슬라이스한다.
/// 2단계 <see cref="AttachAll"/>: 프리팹과 방의 캐릭터에 <see cref="PmdFootShadow"/>를
/// 붙이고 본체·그림자 프레임 짝 목록을 채운다. 반드시 서로 다른 명령으로,
/// 에디트 모드에서 실행할 것 (같은 프레임에 슬라이스와 로드가 겹치면 죽은 참조가 남는다).
/// </summary>
public static class ShadowSetup
{
    /// <summary>대상 시트 이름이 저장소의 다른 동작에서 온 경우.</summary>
    private static readonly Dictionary<(string, string), string> SourceOverrides =
        new Dictionary<(string, string), string> { [("Graveler", "Roll")] = "Special0" };

    // ---------------------------------------------------------------- 1단계 · 슬라이스

    public static string SliceAll()
    {
        var log = new System.Text.StringBuilder();
        foreach ((string species, string thirdParty) in AllSpecies())
            log.AppendLine(SliceOne(species, thirdParty));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>
    /// 한 종의 그림자 시트만 슬라이스한다. 나중에 추가된 종(성원숭)에 쓴다 —
    /// <see cref="SliceAll"/>을 다시 돌리면 기존 종의 스프라이트 ID가 전부 새로 나서
    /// 이미 맺어 둔 본체·그림자 짝이 끊어진다.
    /// </summary>
    public static string SliceOne(string species, string thirdParty)
    {
        string spriteDir = "Assets/Game/Art/Characters/" + species + "/Sprites";
        var animData = PmdCharacterPipeline.LoadAnimData(
            "Assets/ThirdParty/PMDCollab/" + thirdParty + "/Source/AnimData.xml");

        var log = new System.Text.StringBuilder();
        int sliced = 0;
        foreach (string shadowPath in ShadowSheets(spriteDir))
        {
            string target = Path.GetFileNameWithoutExtension(shadowPath); // 예: WalkShadow
            string anim = target.Substring(0, target.Length - "Shadow".Length);
            string source = SourceOverrides.TryGetValue((species, anim), out string s) ? s : anim;
            if (!animData.TryGetValue(source, out PmdCharacterPipeline.AnimEntry entry))
            { log.AppendLine(species + " " + anim + ": AnimData 없음"); continue; }
            SliceGrid(shadowPath, target, entry.frameWidth, entry.frameHeight);
            sliced++;
        }
        log.Append(species + ": 그림자 시트 " + sliced + "장");
        return log.ToString();
    }

    /// <summary>행 수를 시트 높이에서 계산해 슬라이스한다. 잠만보 Sleep처럼 8행이 아닌 시트도 있다.</summary>
    private static void SliceGrid(string sheetPath, string baseName, int frameWidth, int frameHeight)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
        int columns = tex.width / frameWidth;
        int rows = tex.height / frameHeight;

        var rects = new List<SpriteRect>();
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
                rects.Add(new SpriteRect
                {
                    name = baseName + "_" + row + "_" + col,
                    spriteID = GUID.Generate(),
                    rect = new Rect(col * frameWidth, tex.height - (row + 1) * frameHeight,
                                    frameWidth, frameHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });

        provider.SetSpriteRects(rects.ToArray());
        var nameFileId = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameFileId.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- 2단계 · 부착

    public static string AttachAll()
    {
        var log = new System.Text.StringBuilder();

        // 주인공 — 진화로 종이 바뀌므로 세 종의 짝을 전부 싣는다.
        log.AppendLine(AttachToPrefab("Assets/Game/Prefabs/Characters/Player.prefab", null,
            new[] { "Bulbasaur", "Ivysaur", "Venusaur" }));

        // 적 프리팹 — 프리팹에 붙이면 방에 놓인 인스턴스 전부에 퍼진다.
        foreach (string species in new[]
        {
            "Bellsprout", "Caterpie", "Dewgong", "Dragonair", "Dugtrio", "Graveler", "Kingler",
            "Marowak", "Metapod", "Ninetales", "Poliwrath", "Primeape", "Sandslash", "Scyther",
            "Starmie", "Venonat",
        })
            log.AppendLine(AttachToPrefab(
                "Assets/Game/Prefabs/Enemies/Enemy_" + species + ".prefab", null, new[] { species }));
        log.AppendLine(AttachToPrefab(
            "Assets/Game/Prefabs/Enemies/Magikarp_Obstacle.prefab", null, new[] { "Magikarp" }));

        // 방에 사는 보스·NPC.
        var roomResidents = new (string room, string child, string species)[]
        {
            ("Room7_Boss", "Boss_Butterfree", "Butterfree"),
            ("F2Room7_Boss", "Boss_Rhydon", "Rhydon"),
            ("F3Room7_Boss", "Boss_Gyarados", "Gyarados"),
            ("Room3_Event", "Snorlax", "Snorlax"),
            ("F2Room3_Event", "Hitmonlee", "Hitmonlee"),
            ("F2Room3_Event", "Hitmonchan", "Hitmonchan"),
            ("F3Room3_Event", "Lapras", "Lapras"),
        };
        foreach ((string room, string child, string species) in roomResidents)
            log.AppendLine(AttachToPrefab(
                "Assets/Game/Prefabs/Rooms/" + room + ".prefab", child, new[] { species }));

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>적 프리팹 하나에만 그림자를 단다. 나중에 추가된 종(성원숭)에 쓴다.</summary>
    public static string AttachOne(string species)
    {
        string result = AttachToPrefab(
            "Assets/Game/Prefabs/Enemies/Enemy_" + species + ".prefab", null, new[] { species });
        AssetDatabase.SaveAssets();
        return result;
    }

    private static string AttachToPrefab(string prefabPath, string childName, string[] speciesList)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform host = childName == null ? root.transform : FindDeep(root.transform, childName);
            if (host == null) return prefabPath + ": '" + childName + "' 없음";

            SpriteRenderer owner = host.GetComponentInChildren<SpriteRenderer>(true);
            if (owner == null) return prefabPath + ": SpriteRenderer 없음";

            var bodies = new List<Sprite>();
            var shadows = new List<Sprite>();
            foreach (string species in speciesList)
                CollectPairs(species, bodies, shadows);
            if (bodies.Count == 0) return prefabPath + ": 짝을 만들지 못함";

            PmdFootShadow shadow = owner.GetComponent<PmdFootShadow>();
            if (shadow == null) shadow = owner.gameObject.AddComponent<PmdFootShadow>();

            var so = new SerializedObject(shadow);
            so.FindProperty("owner").objectReferenceValue = owner;
            so.FindProperty("shadowColor").colorValue = new Color(0f, 0f, 0f, 0.5f);
            FillArray(so.FindProperty("bodySprites"), bodies);
            FillArray(so.FindProperty("shadowSprites"), shadows);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return Path.GetFileNameWithoutExtension(prefabPath) +
                   (childName != null ? "/" + childName : "") + ": 짝 " + bodies.Count + "개";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>한 종의 모든 시트에서 (본체 프레임, 그림자 프레임) 짝을 모은다.</summary>
    private static void CollectPairs(string species, List<Sprite> bodies, List<Sprite> shadows)
    {
        string spriteDir = "Assets/Game/Art/Characters/" + species + "/Sprites";
        foreach (string shadowPath in ShadowSheets(spriteDir))
        {
            string target = Path.GetFileNameWithoutExtension(shadowPath);
            string anim = target.Substring(0, target.Length - "Shadow".Length);

            var shadowByName = AssetDatabase.LoadAllAssetsAtPath(shadowPath)
                .OfType<Sprite>().ToDictionary(s => s.name);
            foreach (Sprite body in AssetDatabase.LoadAllAssetsAtPath(spriteDir + "/" + anim + ".png")
                         .OfType<Sprite>())
            {
                // "Walk_3_2" → "WalkShadow_3_2". 행이 접힌 시트(잠만보 Sleep처럼 본체 이름이
                // 옛 슬라이스의 잔재로 다른 행 번호를 달고 있는 경우)는 0행으로 대신한다.
                string suffix = body.name.Substring(anim.Length); // "_3_2"
                if (!shadowByName.TryGetValue(target + suffix, out Sprite shadow))
                {
                    string col = suffix.Substring(suffix.LastIndexOf('_')); // "_2"
                    if (!shadowByName.TryGetValue(target + "_0" + col, out shadow)) continue;
                }
                bodies.Add(body);
                shadows.Add(shadow);
            }
        }
    }

    private static void FillArray(SerializedProperty array, List<Sprite> sprites)
    {
        array.arraySize = sprites.Count;
        for (int i = 0; i < sprites.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static IEnumerable<(string species, string thirdParty)> AllSpecies()
    {
        foreach (string dir in Directory.GetDirectories("Assets/ThirdParty/PMDCollab"))
        {
            string basename = Path.GetFileName(dir);
            int underscore = basename.IndexOf('_');
            if (underscore < 0) continue;
            string species = basename.Substring(underscore + 1);
            if (Directory.Exists("Assets/Game/Art/Characters/" + species + "/Sprites"))
                yield return (species, basename);
        }
    }

    private static IEnumerable<string> ShadowSheets(string spriteDir)
    {
        if (!Directory.Exists(spriteDir)) yield break;
        foreach (string file in Directory.GetFiles(spriteDir, "*Shadow.png"))
            yield return file.Replace('\\', '/');
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
