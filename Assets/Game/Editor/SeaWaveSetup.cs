using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 3층 바다의 물 바닥을 파도치게 만드는 도구.
///
/// Sea 타일셋에는 물 애니메이션 프레임이 들어 있지 않다 — 얕은 물 칸(S_12_4·S_13_1·
/// S_13_3·S_13_4·S_13_6·S_14_3)은 여섯 이름이 전부 같은 그림 하나이고, 깊은 물
/// (S_19_1·S_18_15)도 한 픽셀 차이의 같은 그림이다. 그래서 프레임은 원본 타일에서
/// 구워 낸다 (<c>SeaWaves.png</c>): 물결 무늬가 가로로 가장 잘 이어지므로(자기상관이
/// 가로 이동에서 가장 낮다) 한 칸 폭 24픽셀을 24프레임에 걸쳐 오른쪽으로 흘려보낸다.
/// 24프레임이면 제자리로 정확히 돌아와 이음매 없이 계속 흐른다.
///
/// 원본 타일은 이음매가 없어서, 모든 칸을 같은 양만큼 밀면 바다 전체가 한 장의 천처럼
/// 함께 흐른다. 그래서 <see cref="AnimatedTile"/>의 시작 시각을 0으로 두고 속도를 맞춰
/// 모든 칸이 같은 프레임을 보게 한다 — 하나라도 어긋나면 격자가 드러난다.
///
/// 세 단계를 각각 다른 명령으로, 에디트 모드에서 실행할 것.
/// </summary>
public static class SeaWaveSetup
{
    private const string SheetPath = "Assets/Game/Art/Environment/SeaWaves.png";
    private const string TilesRoot = "Assets/Game/Art/Environment/Tiles/";
    private const int FrameSize = 24, FrameCount = 24;

    /// <summary>흐르는 빠르기(초당 프레임). 24프레임이 한 칸이므로 8이면 3초에 한 칸.</summary>
    private const float WaveSpeed = 8f;

    /// <summary>시트의 행 = 애니메이션 타일 하나. 행 순서는 SeaWaves.png를 구운 순서다.</summary>
    private static readonly (string tile, string frame)[] Rows =
    {
        ("S_Water_Shallow", "SeaShallow"),   // 3층 바닥 전체
        ("S_Water_Deep", "SeaDeep"),         // 라프라스 해구
    };

    /// <summary>바꿔 낄 대상. 같은 그림을 쓰던 이름들을 애니메이션 타일 하나로 모은다.</summary>
    private static readonly Dictionary<string, string> Replace = new Dictionary<string, string>
    {
        ["S_12_4"] = "S_Water_Shallow", ["S_13_1"] = "S_Water_Shallow",
        ["S_13_3"] = "S_Water_Shallow", ["S_13_4"] = "S_Water_Shallow",
        ["S_13_6"] = "S_Water_Shallow", ["S_14_3"] = "S_Water_Shallow",
        ["S_19_1"] = "S_Water_Deep", ["S_18_15"] = "S_Water_Deep",
    };

    // ---------------------------------------------------------------- 1단계 · 슬라이스

    public static string SliceFrames()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        if (importer == null) return SheetPath + "가 없다";
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = FrameSize;   // 타일셋과 같아야 한 칸에 딱 맞는다
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        var rects = new List<SpriteRect>();
        for (int row = 0; row < Rows.Length; row++)
            for (int frame = 0; frame < FrameCount; frame++)
                rects.Add(new SpriteRect
                {
                    name = Rows[row].frame + "_" + frame,
                    spriteID = GUID.Generate(),
                    rect = new Rect(frame * FrameSize, tex.height - (row + 1) * FrameSize,
                                    FrameSize, FrameSize),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });

        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return "물결 프레임 " + rects.Count + "장 슬라이스";
    }

    // ---------------------------------------------------------------- 2단계 · 타일 만들기

    public static string CreateTiles()
    {
        var log = new StringBuilder();
        Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
        foreach ((string tileName, string framePrefix) in Rows)
        {
            var frames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                frames[i] = all.FirstOrDefault(s => s.name == framePrefix + "_" + i);
                if (frames[i] == null) return framePrefix + "_" + i + " 스프라이트가 없다 — 슬라이스를 먼저";
            }

            string path = TilesRoot + tileName + ".asset";
            AnimatedTile tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(path);
            bool fresh = tile == null;
            if (fresh) tile = ScriptableObject.CreateInstance<AnimatedTile>();
            tile.m_AnimatedSprites = frames;
            tile.m_MinSpeed = WaveSpeed;
            tile.m_MaxSpeed = WaveSpeed;        // 최소=최대라야 모든 칸이 같은 속도로 흐른다
            tile.m_AnimationStartTime = 0f;     // 시작도 같아야 격자가 드러나지 않는다
            tile.m_TileColliderType = Tile.ColliderType.None;
            if (fresh) AssetDatabase.CreateAsset(tile, path);
            else EditorUtility.SetDirty(tile);
            log.AppendLine(tileName + ": 프레임 " + FrameCount + "장, " + WaveSpeed + "fps");
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ---------------------------------------------------------------- 3단계 · 방에 끼우기

    public static string SwapInRooms()
    {
        var animated = new Dictionary<string, TileBase>();
        foreach ((string tileName, string _) in Rows)
        {
            TileBase t = AssetDatabase.LoadAssetAtPath<TileBase>(TilesRoot + tileName + ".asset");
            if (t == null) return tileName + " 타일이 없다 — 2단계를 먼저";
            animated[tileName] = t;
        }

        var log = new StringBuilder();
        int total = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs/Rooms" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int swapped = 0;
                foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                {
                    var work = new List<(Vector3Int pos, TileBase tile)>();
                    foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
                    {
                        TileBase tile = map.GetTile(pos);
                        if (tile == null || !Replace.TryGetValue(tile.name, out string want)) continue;
                        work.Add((pos, animated[want]));
                    }
                    foreach ((Vector3Int pos, TileBase tile) in work) map.SetTile(pos, tile);
                    swapped += work.Count;
                }
                if (swapped > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.AppendLine("  " + System.IO.Path.GetFileNameWithoutExtension(path) + ": " + swapped + "칸");
                    total += swapped;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        log.AppendLine("합계 " + total + "칸");
        return log.ToString();
    }
}
