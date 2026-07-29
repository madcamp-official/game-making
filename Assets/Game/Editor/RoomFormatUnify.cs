using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 각 층 1번방의 벽·통로 형식을 그 층의 나머지 방에 옮겨, 모든 방의 크기와 좌우 통로를
/// 똑같이 맞춘다.
///
/// 1번방에만 좌우로 길게 뚫린 통로가 있었고(WallMap 1000칸 남짓), 나머지 방은 왼쪽이 막힌
/// 작은 벽(210칸)이었다. 방을 드나드는 연출을 넣으려면 모든 방이 같은 자리에 같은 통로를
/// 가지고 있어야 한다.
///
/// <b>GroundMap은 건드리지 않는다.</b> 방 안쪽 바닥 무늬는 방마다 다른 것이 맞고, 그것까지
/// 베끼면 열두 방이 전부 같은 그림이 된다. 벽에 흩뿌린 장식만은 방마다 자리를 다시 섞어
/// 복사한 티가 나지 않게 한다 — 같은 개수, 같은 종류, 다른 자리다.
/// </summary>
public static class RoomFormatUnify
{
    private class FloorSpec
    {
        public string template;
        public string[] rooms;
    }

    private static readonly FloorSpec[] Floors =
    {
        new FloorSpec
        {
            template = "Room1_Combat",
            rooms = new[] { "Room2_Combat", "Room3_Event", "Room4_Combat",
                            "Room5_Combat", "Room6_Shop", "Room7_Boss" },
        },
        new FloorSpec
        {
            template = "F2Room1_Combat",
            rooms = new[] { "F2Room2_Combat", "F2Room3_Event", "F2Room4_Combat",
                            "F2Room5_Combat", "F2Room6_Shop", "F2Room7_Boss" },
        },
        new FloorSpec
        {
            template = "F3Room1_Combat",
            rooms = new[] { "F3Room2_Combat", "F3Room3_Event", "F3Room4_Combat",
                            "F3Room5_Combat", "F3Room6_Shop", "F3Room7_Boss" },
        },
    };

    private const string RoomDir = "Assets/Game/Prefabs/Rooms/";

    /// <summary>방 안쪽(GroundMap이 덮는 칸). 이 범위는 WallMap이 비어 있어야 한다.</summary>
    private const int InnerMinX = -7, InnerMaxX = 7, InnerMinY = -5, InnerMaxY = 4;

    public static string Apply()
    {
        var log = new StringBuilder();
        foreach (FloorSpec floor in Floors)
        {
            Dictionary<Vector3Int, TileBase> template = ReadWallMap(RoomDir + floor.template + ".prefab");
            if (template == null) { log.AppendLine(floor.template + ": WallMap 없음"); continue; }
            log.AppendLine("[" + floor.template + "] 기준 " + template.Count + "칸");
            // 기준방도 왼쪽 벽은 통짜다. 타일은 그대로 두고 콜라이더만 나눈다 —
            // 여기서 장식까지 다시 섞으면 기준방이 기준이 아니게 된다.
            log.AppendLine("  " + SplitOnly(floor.template));

            foreach (string roomName in floor.rooms)
                log.AppendLine("  " + ApplyTo(roomName, template));
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>타일은 건드리지 않고 왼쪽 벽 콜라이더만 나눈다 (기준방용).</summary>
    private static string SplitOnly(string roomName)
    {
        string path = RoomDir + roomName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!SplitLeftWall(root)) return roomName + ": 왼쪽 벽 이미 나뉨";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return roomName + ": 왼쪽 벽 분할 (타일 유지)";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Dictionary<Vector3Int, TileBase> ReadWallMap(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Tilemap map = FindMap(root, "WallMap");
            if (map == null) return null;
            var tiles = new Dictionary<Vector3Int, TileBase>();
            foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
            {
                TileBase tile = map.GetTile(pos);
                if (tile != null) tiles[pos] = tile;
            }
            return tiles;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string ApplyTo(string roomName, Dictionary<Vector3Int, TileBase> template)
    {
        string path = RoomDir + roomName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Tilemap wall = FindMap(root, "WallMap");
            if (wall == null) return roomName + ": WallMap 없음";

            // 이 방에만 있는 벽 타일(3층 이벤트방의 협곡 연장)을 먼저 떠 둔다.
            Dictionary<Vector3Int, TileBase> keep = CapturePreserved(roomName, wall);

            wall.ClearAllTiles();
            foreach (var pair in Scatter(roomName, template))
                wall.SetTile(pair.Key, pair.Value);

            foreach (var pair in keep)
                wall.SetTile(pair.Key, pair.Value);

            int trimmed = TrimGround(root);
            bool split = SplitLeftWall(root);

            wall.CompressBounds();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return roomName + ": 벽 " + template.Count + "칸" +
                   (keep.Count > 0 ? ", 보존 " + keep.Count + "칸" : "") +
                   (trimmed > 0 ? ", 규격 밖 바닥 " + trimmed + "칸 제거" : "") +
                   (split ? ", 왼쪽 벽 분할" : "");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 방별 예외

    /// <summary>
    /// 3층 이벤트방의 협곡은 방 위아래로 이어져야 한다. 방 밖 벽 구간(x −3..2)에 그려 둔
    /// 협곡 연장 타일을 떠 두었다가 기준 벽을 덮은 뒤 다시 올린다. 넓어진 벽 높이만큼
    /// 위아래로도 같은 무늬를 이어 붙여, 협곡이 벽 한가운데서 잘리지 않게 한다.
    /// </summary>
    private static Dictionary<Vector3Int, TileBase> CapturePreserved(string roomName, Tilemap wall)
    {
        var keep = new Dictionary<Vector3Int, TileBase>();
        if (roomName != "F3Room3_Event") return keep;

        // 방 바깥의 협곡 연장 구간을 그대로 뜬다.
        var byRow = new Dictionary<int, Dictionary<int, TileBase>>();
        foreach (Vector3Int pos in wall.cellBounds.allPositionsWithin)
        {
            if (pos.x < -3 || pos.x > 2) continue;
            if (pos.y >= InnerMinY && pos.y <= InnerMaxY) continue;
            TileBase tile = wall.GetTile(pos);
            if (tile == null) continue;
            keep[pos] = tile;
            if (!byRow.TryGetValue(pos.y, out var row)) byRow[pos.y] = row = new Dictionary<int, TileBase>();
            row[pos.x] = tile;
        }
        if (byRow.Count == 0) return keep;

        // 위아래 끝에서 한 칸 안쪽 줄을 본으로 삼아, 넓어진 벽 끝까지 이어 붙인다.
        // (맨 끝 줄은 방과 맞닿은 모서리 타일이라 반복하면 무늬가 어긋난다.)
        int top = byRow.Keys.Max(), bottom = byRow.Keys.Min();
        Extend(keep, byRow, top, +1, 10);
        Extend(keep, byRow, bottom, -1, -11);
        return keep;
    }

    private static void Extend(Dictionary<Vector3Int, TileBase> keep,
                               Dictionary<int, Dictionary<int, TileBase>> byRow,
                               int from, int step, int until)
    {
        if (!byRow.TryGetValue(from, out var pattern)) return;
        for (int y = from + step; step > 0 ? y <= until : y >= until; y += step)
            foreach (var cell in pattern)
                keep[new Vector3Int(cell.Key, y, 0)] = cell.Value;
    }

    /// <summary>
    /// 통짜였던 왼쪽 벽을 오른쪽과 똑같이 위·아래로 나눠, 가운데 두 칸을 비운다.
    ///
    /// 타일만 뚫어 놓으면 그림으로는 길이 보이는데 몸은 벽에 막힌다. 오른쪽은 이미
    /// Wall_Right_Top / Wall_Right_Bottom이 y ±1을 비우고 그 자리를 ExitDoor가 채우는
    /// 구조였다. 왼쪽도 같은 자리를 비워 두고, 막는 일은 구름이 맡는다 —
    /// 벽으로 막으면 들어오는 연출("왼쪽 길에서 걸어 들어온다")을 할 수가 없다.
    /// </summary>
    private static bool SplitLeftWall(GameObject root)
    {
        Transform left = FindChild(root.transform, "Wall_Left");
        if (left == null) return false;   // 이미 나뉘어 있다

        // 오른쪽을 그대로 비춰 쓴다. 두 쪽이 같은 자리에서 열려야 오갈 때 어긋나지 않는다.
        Transform rightTop = FindChild(root.transform, "Wall_Right_Top");
        Transform rightBottom = FindChild(root.transform, "Wall_Right_Bottom");
        if (rightTop == null || rightBottom == null) return false;

        Make(left, "Wall_Left_Top", rightTop);
        Make(left, "Wall_Left_Bottom", rightBottom);
        Object.DestroyImmediate(left.gameObject);
        return true;
    }

    /// <summary>왼쪽 벽 조각 하나. 원본을 복제해 부품 구성을 그대로 물려받는다.</summary>
    private static void Make(Transform source, string name, Transform mirrorOf)
    {
        GameObject piece = Object.Instantiate(source.gameObject, source.parent);
        piece.name = name;
        piece.transform.localPosition =
            new Vector3(-mirrorOf.localPosition.x, mirrorOf.localPosition.y, mirrorOf.localPosition.z);
        piece.transform.localScale = mirrorOf.localScale;
        piece.transform.localRotation = mirrorOf.localRotation;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    /// <summary>규격(x −7..7, y −5..4) 밖으로 삐져나온 바닥 칸을 지운다.</summary>
    private static int TrimGround(GameObject root)
    {
        Tilemap ground = FindMap(root, "GroundMap");
        if (ground == null) return 0;

        var strays = new List<Vector3Int>();
        foreach (Vector3Int pos in ground.cellBounds.allPositionsWithin)
        {
            if (ground.GetTile(pos) == null) continue;
            if (pos.x < InnerMinX || pos.x > InnerMaxX || pos.y < InnerMinY || pos.y > InnerMaxY)
                strays.Add(pos);
        }
        foreach (Vector3Int pos in strays) ground.SetTile(pos, null);
        if (strays.Count > 0) ground.CompressBounds();
        return strays.Count;
    }

    // ---------------------------------------------------------------- 장식 다시 섞기

    /// <summary>
    /// 기준 벽을 그대로 옮기되, <b>벽 속에 홀로 박힌 장식 타일</b>만 자리를 다시 뽑는다.
    /// 방 열둘이 똑같은 자리에 똑같은 돌을 두고 있으면 복사한 티가 그대로 난다.
    ///
    /// 자리를 옮겨도 되는 것은 사방이 전부 기본 벽(_4_1)인 칸뿐이다. 방 테두리나 통로
    /// 가장자리에 붙은 타일은 구조라서 건드리면 이음매가 깨진다.
    /// </summary>
    private static Dictionary<Vector3Int, TileBase> Scatter(
        string roomName, Dictionary<Vector3Int, TileBase> template)
    {
        var result = new Dictionary<Vector3Int, TileBase>(template);

        var movable = new List<Vector3Int>();
        var plain = new List<Vector3Int>();
        foreach (var pair in template)
        {
            if (!Surrounded(template, pair.Key)) continue;
            if (IsPlainWall(pair.Value)) plain.Add(pair.Key);
            else movable.Add(pair.Key);
        }
        if (movable.Count == 0 || plain.Count == 0) return result;

        TileBase plainTile = template[plain[0]];
        List<TileBase> decorations = movable.Select(p => template[p]).ToList();
        foreach (Vector3Int pos in movable) result[pos] = plainTile;

        // 방 이름으로 씨앗을 만든다. 같은 방은 몇 번을 돌려도 같은 자리에 놓인다.
        var rng = new System.Random(StableHash(roomName));
        var open = new List<Vector3Int>(plain);
        open.AddRange(movable);
        foreach (TileBase decoration in decorations)
        {
            if (open.Count == 0) break;
            int i = rng.Next(open.Count);
            result[open[i]] = decoration;
            open.RemoveAt(i);
        }
        return result;
    }

    /// <summary>사방 이웃이 모두 기본 벽인가. 구조 타일과 흩뿌린 장식을 가른다.</summary>
    private static bool Surrounded(Dictionary<Vector3Int, TileBase> map, Vector3Int pos)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var at = new Vector3Int(pos.x + dx, pos.y + dy, 0);
                if (!map.TryGetValue(at, out TileBase tile) || !IsPlainWall(tile)) return false;
            }
        return true;
    }

    /// <summary>무늬 없는 기본 벽 타일(_4_1)인가.</summary>
    private static bool IsPlainWall(TileBase tile) => tile != null && tile.name.EndsWith("_4_1");

    /// <summary>실행 때마다 같은 값이 나오는 문자열 해시. string.GetHashCode는 보장이 없다.</summary>
    private static int StableHash(string text)
    {
        int hash = 17;
        foreach (char c in text) hash = hash * 31 + c;
        return hash;
    }

    private static Tilemap FindMap(GameObject root, string name)
    {
        foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            if (map.name == name) return map;
        return null;
    }
}
