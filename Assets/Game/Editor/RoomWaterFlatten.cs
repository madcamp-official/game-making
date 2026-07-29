using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 방의 물 지형을 메워 평탄하게 만드는 도구.
///
/// 방마다 호수가 하나씩 박혀 있어서 쓸 수 있는 면적이 갈리고, 밀치기·흡인·해류 같은
/// 강제 이동이 물가에 걸려 어정쩡하게 멈추곤 했다. 물 타일을 그 층의 기본 바닥 타일로
/// 갈고, 물을 막던 PondCollider도 지운다.
///
/// 층마다 타일셋이 달라서 "물"의 번호도 다르다 — 숲(F_)·사막(D_)은 24~26열이 물이고,
/// 바다(S_)는 기본 바닥 자체가 얕은 물빛이라 진한 물은 19_1 한 장뿐이다. 나머지
/// (꽃·조약돌·뼈 같은 장식)는 지형을 막지 않으므로 그대로 둔다.
///
/// 3층 라프라스 이벤트의 해구는 이벤트 연출이 그 위에서 벌어지므로 <see cref="Rooms"/>에
/// 넣지 않았다. 이미 평탄한 방에 다시 돌려도 아무 일도 일어나지 않는다.
/// 에디트 모드에서 실행할 것.
/// </summary>
public static class RoomWaterFlatten
{
    /// <summary>타일셋 접두사별 (기본 바닥 타일, 물 타일 이름 판정).</summary>
    private static bool IsWater(string tileName)
    {
        if (tileName.StartsWith("F_") || tileName.StartsWith("D_"))
            return tileName.StartsWith("F_24_") || tileName.StartsWith("F_25_") ||
                   tileName.StartsWith("F_26_") || tileName.StartsWith("D_24_") ||
                   tileName.StartsWith("D_25_") || tileName.StartsWith("D_26_");
        // 바다 타일셋은 기본 바닥(S_13_*)이 이미 얕은 물빛이다. 진한 물(=호수)은 19_1뿐.
        return tileName == "S_19_1";
    }

    private static readonly string[] Rooms =
    {
        "Room1_Combat", "Room2_Combat", "Room3_Event", "Room4_Combat", "Room5_Combat",
        "F2Room1_Combat", "F2Room2_Combat", "F2Room4_Combat", "F2Room5_Combat",
        "F3Room1_Combat", "F3Room2_Combat", "F3Room4_Combat", "F3Room5_Combat",
    };

    public static string FlattenAll()
    {
        var log = new StringBuilder();
        foreach (string room in Rooms)
            log.AppendLine(Flatten("Assets/Game/Prefabs/Rooms/" + room + ".prefab"));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private static string Flatten(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int filled = 0;
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            {
                // 이 맵이 쓰는 기본 바닥 타일을 맵 안에서 직접 찾는다 — 층마다 타일셋이 다르고,
                // 이름으로 에셋을 뒤지는 것보다 실제로 깔려 있는 타일을 쓰는 편이 확실하다.
                TileBase ground = null;
                var water = new List<Vector3Int>();
                foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
                {
                    TileBase tile = map.GetTile(pos);
                    if (tile == null) continue;
                    if (ground == null && (tile.name == "F_13_1" || tile.name == "D_13_1" ||
                                           tile.name == "S_13_1")) ground = tile;
                    if (IsWater(tile.name)) water.Add(pos);
                }
                if (ground == null) continue;   // 벽 타일맵에는 바닥 타일이 없다

                foreach (Vector3Int pos in water) map.SetTile(pos, ground);
                filled += water.Count;
            }

            int colliders = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.name != "PondCollider") continue;
                Object.DestroyImmediate(child.gameObject);
                colliders++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return System.IO.Path.GetFileNameWithoutExtension(path) +
                   ": 물 타일 " + filled + "칸 메움, PondCollider " + colliders + "개 제거";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
