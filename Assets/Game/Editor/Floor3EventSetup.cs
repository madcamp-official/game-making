using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 3층 이벤트 방(F3Room3_Event)을 잉어킹 폭포에서 라프라스 뱃사공으로 갈아 끼우는 일회성 도구.
///
/// 방 구조: 맵 한가운데를 세로로 가르는 깊은 바다의 계곡(어두운 푸른 타일 + 차단 콜라이더),
/// 계곡 왼쪽에 라프라스. 출구는 오른쪽이므로 어느 선택을 해도 결국 계곡을 건너게 된다.
/// 실행 전에 <see cref="PmdCharacterPipeline.ImportFloor3EventNpcs"/>로 라프라스를 구워 둘 것.
/// (컨트롤러를 다시 구우면 GUID가 바뀌므로 그때는 이 도구도 다시 실행해야 한다.)
/// </summary>
public static class Floor3EventSetup
{
    private const string RoomPath = "Assets/Game/Prefabs/Rooms/F3Room3_Event.prefab";
    private const string TilesRoot = "Assets/Game/Art/Environment/Tiles/";
    private const string LaprasArtRoot = "Assets/Game/Art/Characters/Lapras";

    // 계곡: 셀 x=-2..1(월드 -2..2) 네 칸 폭, 방 세로 전체. 타일 앵커가 (0.5, 0.5)라
    // 셀 (x,y)는 월드 (x+0.5, y+0.5)에 그려진다. 벽 띠를 뚫고 맵 밖까지 이어지는 부분은
    // WallMap에 손으로 그려 두었으므로 여기서는 건드리지 않는다 — 폭만 맞춰 둘 것.
    private const int TrenchCellMinX = -2, TrenchCellMaxX = 1;
    private const int TrenchCellMinY = -5, TrenchCellMaxY = 4;

    // 라프라스는 왼쪽 기슭(x=-2)에서 1.4칸, 도착 자리는 오른쪽 기슭(x=2)에서 같은 만큼
    // 떨어뜨린다. 계곡이 넓어지면 오른쪽 두 자리도 함께 밀린다.
    private static readonly Vector3 LaprasHome = new Vector3(TrenchCellMinX - 1.4f, 0f, 0f);
    private static readonly Vector3 LaprasRideTarget = new Vector3(TrenchCellMaxX + 1f + 1.4f, 0f, 0f);
    private static readonly Vector3 PlayerDropoff = new Vector3(TrenchCellMaxX + 1f + 2.4f, 0f, 0f);

    /// <summary>등에 태우고 미끄러지는 속도(칸/초). 계곡이 넓어져도 이 느낌은 그대로여야 한다.</summary>
    private const float GlideSpeed = 4.46f;

    private static float GlideDuration =>
        Mathf.Round((LaprasRideTarget.x - LaprasHome.x) / GlideSpeed * 10f) / 10f;

    // ---------------------------------------------------------------- 계곡 가장자리 타일

    /// <summary>
    /// 계곡 좌·우 가장자리 타일을 굽는다. Sea 타일셋의 전환 블록(S_15~S_17)은 깊은 물이
    /// 이웃 지형과 만나는 자리를 물거품 경계로 그려 두었는데, 이웃이 비쳐 보일 자리가
    /// 핑크(마젠타) 플레이스홀더로 뚫려 있다. 핑크를 깊은 물 무늬(S_19_1)로 메워 구운 것이
    /// <c>TrenchEdges.png</c>다 — 세로 계곡에는 서(S_15_1)·동(S_17_1) 두 장이면 된다.
    /// 여기서는 그 시트를 슬라이스해 타일 에셋 두 개로 만든다.
    /// </summary>
    public static string SliceTrenchEdges()
    {
        const string sheetPath = "Assets/Game/Art/Environment/TrenchEdges.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
        if (importer == null) return sheetPath + "가 없다";
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 24;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        string[] names = { "S_19_EdgeW", "S_19_EdgeE" };
        var rects = new List<SpriteRect>();
        for (int i = 0; i < names.Length; i++)
            rects.Add(new SpriteRect
            {
                name = names[i],
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

        // 스프라이트와 같은 이름의 타일 에셋. RebuildLaprasRoom이 이 이름으로 집어 쓴다.
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToArray();
        foreach (string name in names)
        {
            string tilePath = TilesRoot + name + ".asset";
            Tile tileAsset = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            bool fresh = tileAsset == null;
            if (fresh) tileAsset = ScriptableObject.CreateInstance<Tile>();
            tileAsset.sprite = sprites.First(s => s.name == name);
            tileAsset.colliderType = Tile.ColliderType.None;
            if (fresh) AssetDatabase.CreateAsset(tileAsset, tilePath);
            else EditorUtility.SetDirty(tileAsset);
        }
        AssetDatabase.SaveAssets();
        return "계곡 가장자리 타일 2장 준비";
    }

    public static string RebuildLaprasRoom()
    {
        GameObject room = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            // 1. 잉어킹 이벤트의 흔적을 지운다. (TreasureChest에 이벤트 스크립트가 붙어 있었다.)
            foreach (string name in new[] { "Magikarp", "TreasureChest", "PondCollider" })
            {
                Transform old = FindChildByName(room.transform, name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            // 2. 옛 연못(어두운 물 타일)을 바닥으로 되돌리고, 한가운데에 계곡을 판다.
            //    좌우 끝 열은 직선으로 잘리지 않게 물거품 가장자리 타일을 쓴다.
            TileBase floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(TilesRoot + "S_13_1.asset");
            TileBase waterTile = AssetDatabase.LoadAssetAtPath<TileBase>(TilesRoot + "S_19_1.asset");
            TileBase edgeWest = AssetDatabase.LoadAssetAtPath<TileBase>(TilesRoot + "S_19_EdgeW.asset");
            TileBase edgeEast = AssetDatabase.LoadAssetAtPath<TileBase>(TilesRoot + "S_19_EdgeE.asset");
            if (floorTile == null || waterTile == null) return "S_13_1/S_19_1 타일 에셋을 찾지 못했다";
            if (edgeWest == null || edgeEast == null)
                return "가장자리 타일이 없다 — SliceTrenchEdges를 먼저 실행할 것";

            Tilemap ground = null;
            foreach (Tilemap map in room.GetComponentsInChildren<Tilemap>())
                if (map.gameObject.name == "GroundMap") ground = map;
            if (ground == null) return "GroundMap을 찾지 못했다";

            int pondCleared = 0;
            foreach (Vector3Int pos in ground.cellBounds.allPositionsWithin)
            {
                TileBase tile = ground.GetTile(pos);
                if (tile == null) continue;
                if (tile.name.StartsWith("S_18_") || tile.name.StartsWith("S_19_") ||
                    tile.name.StartsWith("S_20_"))
                {
                    ground.SetTile(pos, floorTile);
                    pondCleared++;
                }
            }
            int trenchPainted = 0;
            for (int x = TrenchCellMinX; x <= TrenchCellMaxX; x++)
                for (int y = TrenchCellMinY; y <= TrenchCellMaxY; y++)
                {
                    TileBase pick = x == TrenchCellMinX ? edgeWest
                                  : x == TrenchCellMaxX ? edgeEast : waterTile;
                    ground.SetTile(new Vector3Int(x, y, 0), pick);
                    trenchPainted++;
                }

            // 3. 계곡을 몸으로는 못 건너게 막는다. 건너는 연출 동안만 이벤트가 끈다.
            Transform trench = FindChildByName(room.transform, "TrenchCollider");
            if (trench == null)
            {
                trench = new GameObject("TrenchCollider").transform;
                trench.SetParent(room.transform, false);
            }
            trench.localPosition = new Vector3(
                (TrenchCellMinX + TrenchCellMaxX + 1) * 0.5f,
                (TrenchCellMinY + TrenchCellMaxY + 1) * 0.5f, 0f);
            BoxCollider2D trenchBox = trench.GetComponent<BoxCollider2D>();
            if (trenchBox == null) trenchBox = trench.gameObject.AddComponent<BoxCollider2D>();
            trenchBox.size = new Vector2(
                TrenchCellMaxX - TrenchCellMinX + 1,
                TrenchCellMaxY - TrenchCellMinY + 1);

            // 4. 라프라스. 계곡 왼쪽에서 남쪽을 보며 Idle을 반복한다.
            Transform lapras = FindChildByName(room.transform, "Lapras");
            if (lapras == null)
            {
                lapras = new GameObject("Lapras").transform;
                lapras.SetParent(room.transform, false);
            }
            lapras.localPosition = LaprasHome;
            lapras.localScale = Vector3.one;

            Animator animator = lapras.GetComponent<Animator>();
            if (animator == null) animator = lapras.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                LaprasArtRoot + "/Lapras.controller");
            if (animator.runtimeAnimatorController == null)
                return "Lapras.controller가 없다 — 파이프라인을 먼저 실행할 것";

            // 컨트롤러를 먼저 할당한 뒤 스프라이트를 넣는다 (거꾸로 하면 리바인드가 덮어쓴다).
            SpriteRenderer renderer = lapras.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = lapras.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = FindSprite(LaprasArtRoot + "/Sprites/Idle.png", "Idle_0_0");
            if (renderer.sprite == null) return "Idle_0_0 스프라이트를 찾지 못했다";

            EventNpcPose pose = lapras.GetComponent<EventNpcPose>();
            if (pose == null) pose = lapras.gameObject.AddComponent<EventNpcPose>();
            Set(pose, ("initialState", "Idle_0"), ("trainingSpeed", 1f)); // 쉬는 숨은 제 속도로

            // 라프라스 몸통도 길을 막는다. 등이 크니 스승들보다 넓게.
            BoxCollider2D body = lapras.GetComponent<BoxCollider2D>();
            if (body == null) body = lapras.gameObject.AddComponent<BoxCollider2D>();
            body.size = new Vector2(1.2f, 0.8f);
            body.offset = new Vector2(0f, -0.4f);

            // 5. 건너기 연출의 목적지 마커.
            Transform rideTarget = EnsureMarker(room.transform, "LaprasRideTarget", LaprasRideTarget);
            Transform dropoff = EnsureMarker(room.transform, "PlayerDropoff", PlayerDropoff);

            // 6. 이벤트 본체를 라프라스에 붙이고 배선한다.
            LaprasEvent laprasEvent = lapras.GetComponent<LaprasEvent>();
            if (laprasEvent == null) laprasEvent = lapras.gameObject.AddComponent<LaprasEvent>();
            ExitDoor exitDoor = room.GetComponentInChildren<ExitDoor>(true);
            Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Portraits/Lapras.png");
            var so = new SerializedObject(laprasEvent);
            so.FindProperty("prompt").stringValue = "E : 말을 건다";
            so.FindProperty("exitDoor").objectReferenceValue = exitDoor;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("lapras").objectReferenceValue = pose;
            so.FindProperty("laprasRideTarget").objectReferenceValue = rideTarget;
            so.FindProperty("playerDropoff").objectReferenceValue = dropoff;
            so.FindProperty("trenchCollider").objectReferenceValue = trenchBox;
            so.FindProperty("glideDuration").floatValue = GlideDuration;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(room, RoomPath);
            return "라프라스 방 완성: 연못 " + pondCleared + "칸 메움, 계곡 " +
                   (TrenchCellMaxX - TrenchCellMinX + 1) + "칸 폭 " + trenchPainted +
                   "칸, 라프라스 " + LaprasHome + " → " + LaprasRideTarget +
                   " (" + GlideDuration + "초), 하차 " + PlayerDropoff +
                   " (portrait " + (portrait != null) + ", exit " + (exitDoor != null) + ")";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    private static Transform EnsureMarker(Transform root, string name, Vector3 at)
    {
        Transform marker = FindChildByName(root, name);
        if (marker == null)
        {
            marker = new GameObject(name).transform;
            marker.SetParent(root, false);
        }
        marker.localPosition = at;
        return marker;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static Sprite FindSprite(string sheetPath, string name)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        return null;
    }

    private static void Set(Component target, params (string field, object value)[] values)
    {
        var so = new SerializedObject(target);
        foreach ((string field, object value) in values)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
                throw new ArgumentException(target.GetType().Name + "에 " + field + " 필드가 없다");
            switch (value)
            {
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case bool b: prop.boolValue = b; break;
                case string s: prop.stringValue = s; break;
                default: throw new ArgumentException(field + ": 지원하지 않는 형 " + value.GetType());
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
