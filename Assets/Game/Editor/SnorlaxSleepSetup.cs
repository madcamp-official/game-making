using UnityEditor;
using UnityEngine;

/// <summary>
/// 잠만보 머리 위 Zzz 표시를 붙이는 일회성 도구.
///
/// 하는 일이 둘이다.
/// <list type="number">
/// <item><b>임포트 설정을 프로젝트 규칙에 맞춘다.</b> 받아 온 그대로는 Bilinear 필터에
///   PPU 100짜리 Multiple 스프라이트라, 이 게임의 다른 픽셀 아트(Point·PPU 32) 옆에서
///   혼자 뿌옇게 보인다.</item>
/// <item>그 스프라이트를 <see cref="SnorlaxEvent"/>에 연결한다.</item>
/// </list>
///
/// <c>sleep</c> 폴더의 000~007은 <b>여덟 방향</b>이다 (0=아래, 2=오른쪽, 4=위, 6=왼쪽 —
/// 이 프로젝트의 <c>Idle_0</c>·<c>Idle_2</c> 규칙과 같다). 자는 표시는 위로 피어올라야 하므로
/// 4번을 쓴다. 실제로 000은 아래로 흐르고 004는 작은 z에서 큰 Z로 올라간다.
/// </summary>
public static class SnorlaxSleepSetup
{
    /// <summary>위(북)로 피어오르는 Zzz.</summary>
    private const string SleepSpritePath = "Assets/Game/Art/Environment/sleep/004/000.png";

    private const string RoomPrefabPath = "Assets/Game/Prefabs/Rooms/Room3_Event.prefab";

    /// <summary>이 게임의 픽셀 아트 공통값 (docs/project-standards.md).</summary>
    private const int PixelsPerUnit = 32;

    [MenuItem("Game/잠만보 잠 표시 연결")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new System.Text.StringBuilder();

        if (!FixImport(log)) return log.ToString();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SleepSpritePath);
        if (sprite == null)
        {
            log.AppendLine("스프라이트를 읽지 못했다: " + SleepSpritePath);
            return log.ToString();
        }
        log.AppendLine("Zzz 스프라이트 = " + sprite.name + "  월드 크기 " + sprite.bounds.size.ToString("F2"));

        var room = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabPath);
        if (room == null) { log.AppendLine("방 프리팹이 없다: " + RoomPrefabPath); return log.ToString(); }

        var ev = room.GetComponentInChildren<SnorlaxEvent>(true);
        if (ev == null) { log.AppendLine("SnorlaxEvent가 없다: " + RoomPrefabPath); return log.ToString(); }

        // 프리팹 안의 값을 직접 고친다. SerializedObject를 쓰는 이유는 private 필드라서다 —
        // 인스펙터에 내보내는 것과 같은 길이므로 Undo·저장이 정상으로 걸린다.
        var so = new SerializedObject(ev);
        SerializedProperty prop = so.FindProperty("sleepSprite");
        if (prop == null) { log.AppendLine("sleepSprite 필드를 찾지 못했다"); return log.ToString(); }

        prop.objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ev);
        PrefabUtility.SavePrefabAsset(room);
        AssetDatabase.SaveAssets();

        log.AppendLine(room.name + " 의 SnorlaxEvent에 연결 완료");
        return log.ToString();
    }

    /// <summary>
    /// 픽셀 아트 규칙에 맞춘다: 점 필터, 압축 없음, 밉맵 없음, PPU 32, 한 장짜리.
    ///
    /// Multiple에서 Single로 바꾸는 것은 잘라 둔 조각(<c>000_0</c>)을 버리는 일이지만,
    /// 아직 그 조각을 참조하는 곳이 없고 표시 크기는 코드가 몸에 맞춰 다시 잡으므로
    /// (<see cref="SleepMark"/>) 통짜로 두는 편이 다루기 쉽다.
    /// </summary>
    private static bool FixImport(System.Text.StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(SleepSpritePath) as TextureImporter;
        if (importer == null) { log.AppendLine("그림이 없다: " + SleepSpritePath); return false; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platform);

        importer.SaveAndReimport();
        log.AppendLine("임포트 설정 정리: Point · 압축 없음 · PPU " + PixelsPerUnit + " · Single");
        return true;
    }
}
