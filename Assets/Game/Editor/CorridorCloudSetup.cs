using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 통로를 막는 구름 시트를 자르고, 그 프레임들을 <see cref="RoomFlowController"/>에 물려 준다.
///
/// CorridorCloud.png는 가로로 이어지는 띠에서 한 칸씩 밀린 48×48 프레임 열여섯 장이다
/// (scratchpad/bake_cloud.py가 굽는다). 순서대로 돌리면 구름이 흘러가고, 마지막에서
/// 첫 장으로 돌아가도 이음매가 없다.
///
/// 두 단계는 서로 다른 명령으로, 에디트 모드에서 실행할 것 — 같은 프레임에 자르고
/// 읽으면 죽은 참조가 남는다.
/// </summary>
public static class CorridorCloudSetup
{
    private const string Sheet = "Assets/Game/Art/Environment/CorridorCloud.png";
    private const int FrameSize = 48;

    // ---------------------------------------------------------------- 1단계 · 자르기

    public static string Slice()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
        if (importer == null) return "CorridorCloud.png가 없다 — bake_cloud.py를 먼저 돌릴 것";

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 24;   // 바닥 타일과 같은 배율 → 한 프레임이 2x2칸
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
        int count = tex.width / FrameSize;

        var rects = new List<SpriteRect>();
        for (int i = 0; i < count; i++)
            rects.Add(new SpriteRect
            {
                name = "CorridorCloud_" + i,
                spriteID = GUID.Generate(),
                rect = new Rect(i * FrameSize, 0f, FrameSize, tex.height),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            });

        provider.SetSpriteRects(rects.ToArray());
        var nameFileId = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameFileId.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        return "구름 프레임 " + count + "장 슬라이스";
    }

    // ---------------------------------------------------------------- 2단계 · 물려 주기

    /// <summary>씬의 RoomFlowController에 프레임 목록을 채운다.</summary>
    public static string Wire()
    {
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Sheet)
            .OfType<Sprite>()
            // "CorridorCloud_10"이 "_2"보다 앞에 오지 않도록 숫자로 정렬한다.
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
        if (frames.Length == 0) return "슬라이스된 프레임이 없다 — Slice를 먼저";

        var flow = Object.FindAnyObjectByType<RoomFlowController>();
        if (flow == null) return "씬에서 RoomFlowController를 찾지 못했다";

        var so = new SerializedObject(flow);
        SerializedProperty array = so.FindProperty("corridorCloudFrames");
        if (array == null) return "corridorCloudFrames 필드가 없다";
        array.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        EditorSceneManager.SaveScene(flow.gameObject.scene);
        return "RoomFlowController에 구름 프레임 " + frames.Length + "장 연결 (씬 저장)";
    }
}
