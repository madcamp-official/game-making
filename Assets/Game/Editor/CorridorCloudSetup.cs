using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 통로를 메우는 뭉게구름 그림을 들여오고 <see cref="RoomFlowController"/>에 물려 준다.
///
/// CorridorCloud.png는 240×72(10칸×3칸) 정적인 구름 한 장이다(scratchpad의
/// bake_cloudbank.py가 굽는다). 예전에는 흘러가는 프레임 열여섯 장을 잘라 돌렸는데,
/// 통로를 꽉 채운 구름이 움직이면 벽이 아니라 물살처럼 보여 정적인 한 장으로 바꿨다.
/// </summary>
public static class CorridorCloudSetup
{
    private const string Path = "Assets/Game/Art/Environment/CorridorCloud.png";

    // ---------------------------------------------------------------- 1단계 · 들여오기

    public static string Import()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        if (importer == null) return "CorridorCloud.png가 없다 — bake_cloudbank.py를 먼저 돌릴 것";

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;   // 프레임을 자르지 않는다
        importer.spritePixelsPerUnit = 24;   // 바닥 타일과 같은 배율 → 10칸 x 3칸
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return "구름 뱅크 들여오기 완료";
    }

    // ---------------------------------------------------------------- 2단계 · 물려 주기

    /// <summary>씬의 RoomFlowController에 구름 스프라이트를 채운다.</summary>
    public static string Wire()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path);
        if (sprite == null) return "스프라이트가 없다 — Import를 먼저";

        var flow = Object.FindAnyObjectByType<RoomFlowController>();
        if (flow == null) return "씬에서 RoomFlowController를 찾지 못했다";

        var so = new SerializedObject(flow);
        SerializedProperty prop = so.FindProperty("corridorCloudSprite");
        if (prop == null) return "corridorCloudSprite 필드가 없다";
        prop.objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        EditorSceneManager.SaveScene(flow.gameObject.scene);
        return "RoomFlowController에 구름 연결 (씬 저장)";
    }
}
