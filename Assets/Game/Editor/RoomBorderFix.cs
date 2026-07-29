using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 방 테두리의 바깥 모서리 타일을 제자리에 맞는 것으로 고치는 도구.
///
/// 타일셋의 울타리(절벽) 조각은 역할이 정해져 있다 — 위·아래·좌·우 가장자리 넷,
/// 방이 바깥으로 꺾이는 볼록 모서리 넷(3_15 좌상, 4_15 우상, 3_16 좌하, 4_16 우하),
/// 안으로 꺾이는 오목 모서리 넷(3_0·3_2·5_0·5_2). 그런데 여러 방에서 볼록 자리에
/// 오목 조각이 들어가 있어 네 귀퉁이의 울타리가 끊겨 보였다.
///
/// 고칠 자리는 이름이 아니라 <em>모양</em>으로 찾는다. 어떤 벽 칸의 직교 이웃 넷이 모두
/// 벽이고 대각 하나만 방 안쪽이면, 그 칸은 그 방향의 볼록 모서리다. 그래서 방마다 크기나
/// 통로 모양이 달라도(1층 1번 방의 복도, 3번 방의 벽감) 알아서 맞는다.
///
/// 가장자리가 아닌 칸(바깥 채움 4_1, 나무 장식 7_*·10_*)과 문 옆 마감은 건드리지 않는다.
/// 에디트 모드에서 실행할 것.
/// </summary>
public static class RoomBorderFix
{
    /// <summary>울타리·나무 계열 행. 12행부터는 바닥과 장식이라 '방 안쪽'으로 친다.</summary>
    private static readonly HashSet<int> WallRows = new HashSet<int> { 3, 4, 5, 7, 10 };

    /// <summary>다시 깔아도 되는 테두리 조각. 장식 변형은 여기 없으므로 그대로 남는다.</summary>
    private static readonly HashSet<string> Border = new HashSet<string>
    {
        "4_0", "4_2", "3_1", "5_1",          // 아래·위·오른·왼 가장자리
        "3_15", "4_15", "3_16", "4_16",      // 볼록 모서리
        "3_0", "3_2", "5_0", "5_2",          // 오목 모서리
    };

    public static string FixAll()
    {
        var log = new StringBuilder();
        int total = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs/Rooms" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            int fixedCells = FixRoom(path, log);
            total += fixedCells;
        }
        AssetDatabase.SaveAssets();
        log.AppendLine("합계 " + total + "칸");
        return log.ToString();
    }

    private static int FixRoom(string path, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int changed = 0;
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            {
                BoundsInt bounds = map.cellBounds;
                string prefix = null;
                foreach (Vector3Int pos in bounds.allPositionsWithin)
                {
                    TileBase t = map.GetTile(pos);
                    if (t != null && IsWall(t.name)) { prefix = t.name.Substring(0, 2); break; }
                }
                if (prefix == null) continue;   // 벽 조각이 없는 타일맵(바닥)은 대상이 아니다

                var repaint = new List<(Vector3Int pos, string want)>();
                foreach (Vector3Int pos in bounds.allPositionsWithin)
                {
                    TileBase tile = map.GetTile(pos);
                    if (tile == null || !Border.Contains(Suffix(tile.name))) continue;

                    // 직교 이웃 중 하나라도 안쪽이면 가장자리이지 볼록 모서리가 아니다.
                    if (Inside(map, bounds, pos.x, pos.y + 1) || Inside(map, bounds, pos.x, pos.y - 1) ||
                        Inside(map, bounds, pos.x + 1, pos.y) || Inside(map, bounds, pos.x - 1, pos.y))
                        continue;

                    string want =
                        Inside(map, bounds, pos.x + 1, pos.y - 1) ? "3_15" :   // 안쪽이 우하 → 좌상 모서리
                        Inside(map, bounds, pos.x - 1, pos.y - 1) ? "4_15" :   // 좌하 → 우상
                        Inside(map, bounds, pos.x + 1, pos.y + 1) ? "3_16" :   // 우상 → 좌하
                        Inside(map, bounds, pos.x - 1, pos.y + 1) ? "4_16" : null;
                    if (want == null || want == Suffix(tile.name)) continue;
                    repaint.Add((pos, prefix + want));
                }

                foreach ((Vector3Int pos, string want) in repaint)
                {
                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(
                        "Assets/Game/Art/Environment/Tiles/" + want + ".asset");
                    if (tile == null) { log.AppendLine("  " + want + " 에셋 없음"); continue; }
                    log.AppendLine("  " + System.IO.Path.GetFileNameWithoutExtension(path) +
                                   " " + pos.x + "," + pos.y + ": " + map.GetTile(pos).name + " → " + want);
                    map.SetTile(pos, tile);
                    changed++;
                }
            }

            if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool IsWall(string name)
    {
        string[] parts = name.Split('_');
        return parts.Length == 3 && int.TryParse(parts[1], out int row) && WallRows.Contains(row);
    }

    private static string Suffix(string name) => name.Substring(2);

    /// <summary>방 안쪽인가. 타일맵 바깥은 안쪽이 아니고, 비었거나 바닥 계열이면 안쪽이다.</summary>
    private static bool Inside(Tilemap map, BoundsInt bounds, int x, int y)
    {
        if (x < bounds.xMin || x > bounds.xMax - 1 || y < bounds.yMin || y > bounds.yMax - 1) return false;
        TileBase tile = map.GetTile(new Vector3Int(x, y, 0));
        return tile == null || !IsWall(tile.name);
    }
}
