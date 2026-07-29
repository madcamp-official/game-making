using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 전투방 출구를 막는 넘어진 통나무를 굽고 방에 끼운다.
///
/// 출구는 예전부터 흰 사각형에 색만 입힌 것이었다 — 닫히면 갈색, 열리면 초록. 무엇이
/// 막고 있는지가 그림으로 보이지 않아 "왜 못 지나가는가"가 읽히지 않았다.
/// <c>ForestLogGate.png</c>는 그 자리에 놓을 넘어진 통나무 두 장이다: 성한 통나무와,
/// 가운데가 부러져 위아래로 벌어진 통나무.
///
/// 충돌체는 손대지 않는다. <see cref="ExitDoor"/>는 열려도 단단해야 하고(벽에 구멍이
/// 생기면 플레이어가 방 밖으로 걸어 나간다) 닿는 것으로 다음 방에 간다. 그림만 바뀐다.
///
/// 그래서 크기를 옮겨 담는 손질이 하나 필요하다. 지금 출구는 1x1 사각형 스프라이트를
/// 트랜스폼 배율 (0.5, 2)로 늘여 쓰는데, 여기에 통나무 그림을 넣으면 같이 찌그러진다.
/// 배율을 1로 되돌리고 그만큼을 BoxCollider2D의 size로 옮겨, 충돌 범위는 한 치도 바뀌지
/// 않은 채 그림만 제 비율로 서게 한다.
/// </summary>
public static class LogGateSetup
{
    private const string SheetPath = "Assets/Game/Art/Environment/ForestLogGate.png";

    /// <summary>시트에 구운 순서.</summary>
    private static readonly string[] Frames = { "LogGate_Closed", "LogGate_Open" };

    // ---------------------------------------------------------------- 1단계 · 슬라이스

    public static string SliceSheet()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        if (importer == null) return SheetPath + "가 없다";
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 24;      // 타일과 같은 척도. 18x48px = 0.75 x 2칸
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

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
                rect = new Rect(i * 18, 0, 18, 48),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            });
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return "통나무 문 " + Frames.Length + "장 슬라이스";
    }

    // ---------------------------------------------------------------- 2단계 · 방에 끼우기

    public static string ApplyTo(string roomName)
    {
        string roomPath = "Assets/Game/Prefabs/Rooms/" + roomName + ".prefab";
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
        Sprite closed = sprites.FirstOrDefault(s => s.name == Frames[0]);
        Sprite open = sprites.FirstOrDefault(s => s.name == Frames[1]);
        if (closed == null || open == null) return "스프라이트가 없다 — 1단계를 먼저";

        GameObject root = PrefabUtility.LoadPrefabContents(roomPath);
        try
        {
            ExitDoor door = root.GetComponentInChildren<ExitDoor>(true);
            if (door == null) return roomName + ": ExitDoor를 찾지 못했다";

            // 배율로 늘여 쓰던 크기를 충돌체로 옮긴다 — 충돌 범위는 그대로, 그림만 제 비율로.
            Transform t = door.transform;
            var box = door.GetComponent<BoxCollider2D>();
            Vector2 before = new Vector2(box.size.x * t.localScale.x, box.size.y * t.localScale.y);
            box.size = before;
            box.offset = new Vector2(box.offset.x * t.localScale.x, box.offset.y * t.localScale.y);
            t.localScale = Vector3.one;

            var renderer = door.GetComponentInChildren<SpriteRenderer>(true);
            renderer.sprite = closed;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;   // 늘이지 않고 제 픽셀 크기로

            var so = new SerializedObject(door);
            so.FindProperty("closedSprite").objectReferenceValue = closed;
            so.FindProperty("openSprite").objectReferenceValue = open;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, roomPath);
            return roomName + ": 통나무 문 적용 (충돌 " + before + " 유지)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
