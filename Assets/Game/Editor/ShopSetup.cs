using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 세 층의 상점을 켈리몬이 지키는 한 가게로 통일한다.
///
/// 바닥에 카펫을 <b>가로 한 줄</b>로 깔고, 맨 왼쪽 칸에 켈리몬을 세우고 그 오른쪽으로
/// 상품 넷을 늘어놓는다. 층이 달라도 같은 상인이 같은 자리에서 장사하는 것으로 읽혀야 해서
/// 세 방을 똑같이 만든다.
///
/// 단계마다 다른 명령으로, 에디트 모드에서 실행할 것 (슬라이스와 로드가 같은 프레임에
/// 겹치면 죽은 참조가 남는다 — progress.md 참고).
/// </summary>
public static class ShopSetup
{
    private const string EnvDir = "Assets/Game/Art/Environment/";
    private const string TileDir = EnvDir + "Tiles/";
    private const string CarpetTile = TileDir + "Carpet.asset";

    private static readonly string[] ShopRooms = { "Room6_Shop", "F2Room6_Shop", "F3Room6_Shop" };

    /// <summary>카펫을 깔 줄. 타일 (x, y)는 월드 [x, x+1] × [y, y+1]을 덮는다.</summary>
    private const int CarpetY = 1;
    private const int CarpetMinX = -5, CarpetMaxX = 3;

    /// <summary>켈리몬은 맨 왼쪽 카펫 칸 위에 선다.</summary>
    private static readonly Vector3 KeeperPos = new Vector3(CarpetMinX + 0.5f, CarpetY + 0.8f, 0f);

    /// <summary>상품 넷의 x. 켈리몬 오른쪽으로 두 칸씩 띄운다.</summary>
    private static readonly float[] SlotX = { -3f, -1f, 1f, 3f };
    /// <summary>상품 그림. 카펫 띠(y 1~2)의 가운데에 얹는다.</summary>
    private const float SlotY = 1.8f;

    // ---------------------------------------------------------------- 1단계 · 카펫 타일

    /// <summary>carpet.png를 타일 하나로 만든다. 24×24 한 장이라 격자로 자를 것이 없다.</summary>
    public static string MakeCarpetTile()
    {
        string sheet = EnvDir + "carpet.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
        if (importer == null) return "carpet.png가 없다";

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 24;    // 바닥 타일과 같은 배율
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        // 타일맵은 FullRect가 아니면 칸을 꽉 채우지 못한다.
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sheet);
        if (sprite == null) return "carpet 스프라이트를 읽지 못했다";

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(CarpetTile);
        bool created = tile == null;
        if (created) tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;   // 밟고 지나가는 깔개다
        if (created) AssetDatabase.CreateAsset(tile, CarpetTile);
        else EditorUtility.SetDirty(tile);

        AssetDatabase.SaveAssets();
        return "Carpet 타일 " + (created ? "생성" : "갱신");
    }

    // ---------------------------------------------------------------- 2단계 · 켈리몬 시트

    public static string ImportKecleon()
    {
        return PmdCharacterPipeline.Import(new PmdCharacterPipeline.CharacterSpec(
            "Kecleon", "0352_Kecleon",
            new PmdCharacterPipeline.AnimSpec("Idle", "Idle", true),
            // 자세 잡기와 숨 들이쉬기는 한 번만. 끄덕임만 반복이다.
            new PmdCharacterPipeline.AnimSpec("Pose", "Pose", false),
            new PmdCharacterPipeline.AnimSpec("Nod", "Nod", true),
            new PmdCharacterPipeline.AnimSpec("DeepBreath", "DeepBreath", false)));
    }

    // ---------------------------------------------------------------- 3단계 · 방 꾸미기

    public static string SetupRooms()
    {
        var carpet = AssetDatabase.LoadAssetAtPath<Tile>(CarpetTile);
        if (carpet == null) return "Carpet 타일이 없다 — MakeCarpetTile 먼저";

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Game/Art/Characters/Kecleon/Kecleon.controller");
        if (controller == null) return "켈리몬 컨트롤러가 없다 — ImportKecleon 먼저";
        Sprite firstFrame = FindSprite("Assets/Game/Art/Characters/Kecleon/Sprites/Nod.png", "Nod_0_0");

        var log = new StringBuilder();
        foreach (string roomName in ShopRooms)
            log.AppendLine(SetupRoom(roomName, carpet, controller, firstFrame));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private static string SetupRoom(string roomName, Tile carpet,
                                    RuntimeAnimatorController controller, Sprite firstFrame)
    {
        string path = "Assets/Game/Prefabs/Rooms/" + roomName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 카펫 — 가로 한 줄. 바닥 위에 덧칠하므로 기존 무늬가 양옆으로 그대로 남는다.
            Tilemap ground = FindMap(root, "GroundMap");
            if (ground == null) return roomName + ": GroundMap 없음";
            int laid = 0;
            for (int x = CarpetMinX; x <= CarpetMaxX; x++)
            {
                ground.SetTile(new Vector3Int(x, CarpetY, 0), carpet);
                laid++;
            }

            // 상품 넷과 받침대를 카펫 위로 옮긴다.
            int moved = 0;
            for (int i = 0; i < SlotX.Length; i++)
            {
                Transform slot = FindChild(root.transform, "ShopSlot" + i);
                if (slot != null)
                {
                    slot.localPosition = new Vector3(SlotX[i], SlotY, 0f);
                    moved++;
                }
                // 받침대는 걷어낸다. 모래 바닥에 놓을 때는 진열대 노릇을 했지만, 카펫 위에서는
                // 갈색 널판이 깔개를 가로질러 진흙처럼 보인다. 이제 카펫이 진열대다.
                Transform pedestal = FindChild(root.transform, "SlotPedestal" + i);
                if (pedestal != null) Object.DestroyImmediate(pedestal.gameObject);
            }

            // 켈리몬 — 맨 왼쪽 카펫 칸.
            Transform keeper = FindChild(root.transform, "Kecleon");
            if (keeper == null)
            {
                var go = new GameObject("Kecleon");
                go.transform.SetParent(root.transform, false);
                keeper = go.transform;
            }
            keeper.localPosition = KeeperPos;
            keeper.localScale = Vector3.one;

            // ⚠ 컨트롤러를 먼저, 스프라이트를 나중에. 순서가 반대면 애니메이터가 다시
            // 바인딩되며 처음 기록해 둔 기본값을 SpriteRenderer에 도로 써 넣는다.
            Animator animator = keeper.GetComponent<Animator>();
            if (animator == null) animator = keeper.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            SpriteRenderer sr = keeper.GetComponent<SpriteRenderer>();
            if (sr == null) sr = keeper.gameObject.AddComponent<SpriteRenderer>();
            if (firstFrame != null) sr.sprite = firstFrame;
            sr.sortingOrder = 10;   // 캐릭터 층

            // 몸으로 막는다. Rigidbody가 없는 정적 콜라이더라 밀리지 않는다.
            BoxCollider2D box = keeper.GetComponent<BoxCollider2D>();
            if (box == null) box = keeper.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 0.5f);
            box.offset = new Vector2(0f, -0.3f);   // 발치만 막아 머리 위로는 지나가 보인다

            if (keeper.GetComponent<ShopKeeper>() == null)
                keeper.gameObject.AddComponent<ShopKeeper>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return roomName + ": 카펫 " + laid + "칸, 상품 " + moved + "개 재배치, 켈리몬 배치";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 4단계 · 그림자

    /// <summary>켈리몬에게 발밑 그림자를 단다. 3단계와 다른 명령으로 실행할 것.</summary>
    public static string AttachKecleonShadow()
    {
        var log = new StringBuilder();
        foreach (string roomName in ShopRooms)
            log.AppendLine(ShadowSetup.AttachToRoomChild(roomName, "Kecleon", "Kecleon"));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ---------------------------------------------------------------- 도구

    private static Sprite FindSprite(string sheetPath, string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        return null;
    }

    private static Tilemap FindMap(GameObject root, string name)
    {
        foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            if (map.name == name) return map;
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
