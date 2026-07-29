using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 1층(숲) 방에 다음 방으로 이어지는 흙길을 깐다.
///
/// 숲 타일셋에는 길이 없다 — 울타리·풀·물뿐이다. 그래서 풀 타일(F_13_1)을 어둡게 낮춰
/// 다져진 흙으로 구운 것이 <c>ForestPath.png</c>다. 풀의 잔디 결을 그대로 두고 색만
/// 흙빛으로 옮겼기 때문에 길과 풀이 같은 결을 공유해 이질감이 없고, 경계는 자로 그은
/// 직선이 아니라 픽셀 단위로 들쭉날쭉하게 + 풀포기가 성글게 침범하는 디더 띠를 얹어
/// 밟혀서 생긴 길처럼 보이게 했다.
///
/// 시트에는 여섯 장이 있다. 길은 문 높이에 맞춰 두 칸이므로 윗줄(N)과 아랫줄(S)이 있고,
/// 각각 요철 위상이 다른 두 벌(0·1)을 번갈아 깔아 반복이 눈에 띄지 않게 한다. 서쪽 끝은
/// 길이 풀 속으로 스며들며 사라지는 마감(NW·SW)이다.
///
/// 슬라이스와 적용은 서로 다른 명령으로, 에디트 모드에서 실행할 것.
/// </summary>
public static class ForestPathSetup
{
    private const string SheetPath = "Assets/Game/Art/Environment/ForestPath.png";
    private const string TilesRoot = "Assets/Game/Art/Environment/Tiles/";

    /// <summary>시트에 구운 순서. 슬라이스 이름이자 타일 에셋 이름이다.</summary>
    private static readonly string[] Frames =
    { "F_PathN0", "F_PathS0", "F_PathN1", "F_PathS1", "F_PathNW", "F_PathSW" };

    /// <summary>길이 지나는 자리. 문이 y=-1..0 두 칸이라 길도 두 칸 높이다.</summary>
    private const int PathMinX = 2, PathMaxX = 7, PathBottomY = -1, PathTopY = 0;

    // ---------------------------------------------------------------- 1단계 · 슬라이스

    public static string SliceAndCreateTiles()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        if (importer == null) return SheetPath + "가 없다";
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 24;      // 타일셋과 같아야 한 칸에 딱 맞는다
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int i = 0; i < Frames.Length; i++)
            rects.Add(new SpriteRect
            {
                name = Frames[i],
                spriteID = GUID.Generate(),
                rect = new Rect(i * 24, 0, 24, 24),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            });
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
        foreach (string name in Frames)
        {
            string path = TilesRoot + name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            bool fresh = tile == null;
            if (fresh) tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprites.First(s => s.name == name);
            tile.colliderType = Tile.ColliderType.None;   // 길은 그냥 바닥이다
            if (fresh) AssetDatabase.CreateAsset(tile, path);
            else EditorUtility.SetDirty(tile);
        }
        AssetDatabase.SaveAssets();
        return "흙길 타일 " + Frames.Length + "장 준비";
    }

    // ---------------------------------------------------------------- 2단계 · 방에 깔기

    public static string ApplyToRoom2()
    {
        const string roomPath = "Assets/Game/Prefabs/Rooms/Room2_Combat.prefab";
        var tiles = new Dictionary<string, TileBase>();
        foreach (string name in Frames)
        {
            TileBase t = AssetDatabase.LoadAssetAtPath<TileBase>(TilesRoot + name + ".asset");
            if (t == null) return name + " 타일이 없다 — 1단계를 먼저";
            tiles[name] = t;
        }
        TileBase grass = AssetDatabase.LoadAssetAtPath<TileBase>(TilesRoot + "F_13_1.asset");
        if (grass == null) return "F_13_1 타일이 없다";

        GameObject root = PrefabUtility.LoadPrefabContents(roomPath);
        try
        {
            Tilemap ground = null;
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                if (map.name == "GroundMap") ground = map;
            if (ground == null) return "GroundMap을 찾지 못했다";

            // 임시로 들어와 있던 다른 층 타일(사막 D_)을 전부 원래 풀로 되돌린다.
            int restored = 0;
            foreach (Vector3Int pos in ground.cellBounds.allPositionsWithin)
            {
                TileBase tile = ground.GetTile(pos);
                if (tile == null || !tile.name.StartsWith("D_")) continue;
                ground.SetTile(pos, grass);
                restored++;
            }

            // 그 위에 숲 흙길을 깐다. 서쪽 끝 한 칸은 마감, 나머지는 두 벌을 번갈아.
            int painted = 0;
            for (int x = PathMinX; x <= PathMaxX; x++)
            {
                bool cap = x == PathMinX;
                string variant = ((x - PathMinX) % 2).ToString();
                ground.SetTile(new Vector3Int(x, PathTopY, 0),
                               tiles[cap ? "F_PathNW" : "F_PathN" + variant]);
                ground.SetTile(new Vector3Int(x, PathBottomY, 0),
                               tiles[cap ? "F_PathSW" : "F_PathS" + variant]);
                painted += 2;
            }

            PrefabUtility.SaveAsPrefabAsset(root, roomPath);
            return "Room2_Combat: 사막 타일 " + restored + "칸 풀로 복구, 흙길 " + painted + "칸";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
