using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 타이틀 배경으로 흐를 <b>맵 스틸</b>을 굽는다. <c>scene1~3</c> 프리팹(층마다 하나 — 숲·사막·바다)을
/// 하나씩 세워 카메라로 찍어 <c>Resources/UI/TitleMaps/</c>에 넣어 두고,
/// <see cref="TitleMapBackdrop"/>이 한 장씩 밀며 다음 장으로 넘긴다.
///
/// 왜 실시간으로 맵을 띄우지 않는가: 이 프리팹에는 적과 그 능력 스크립트가 함께 들어 있다.
/// 타이틀 뒤에서 그것들을 돌리면 적이 돌아다니고 장판을 깔고 탄을 쏜다 — 게다가 플레이어를
/// 찾아 헤맨다. <b>판이 언제 시작됐는지가 흐려진다</b>는 이유로 <see cref="RoomFlowController"/>가
/// 일부러 타이틀 동안 방을 올리지 않는데, 배경으로 되살리면 그 결정을 뒷문으로 무르는 셈이다.
/// 그림 한 장으로 굽고 나면 타이틀은 아무것도 계산하지 않는다.
///
/// 에디터에서 굽는 것이 요점이다. 재생 모드가 아니면 <c>Awake</c>·<c>Start</c>가 돌지 않아
/// 프리팹은 <b>배치된 그대로</b> 찍힌다 — 적은 첫 프레임 자세로 서 있고 아무 스크립트도 돌지 않는다.
///
/// <b>화면보다 넓게 찍는다</b>(30유닛). 화면에 꼭 맞게 찍으면 밀 자리가 없어 패닝이 되지 않는다.
/// 카메라 크기는 게임 카메라와 같은 <c>5.625</c>라 유닛당 24픽셀이고, 그래야 <b>타일 한 칸이
/// 정확히 24픽셀</b>로 찍혀 화면에서 정수배로 늘릴 때 원본 픽셀 격자가 살아난다.
/// </summary>
public static class TitleMapSetup
{
    private const string Folder = "Assets/Game/Resources/UI/TitleMaps/";

    /// <summary>
    /// 찍을 맵. 층마다 하나씩, 타이틀에서 이 순서로 넘어간다.
    ///
    /// 방 프리팹 스물한 개를 전부 찍던 때도 있었지만, 배경으로 흐르기에는 같은 방이 되풀이돼
    /// 층이 바뀐다는 느낌만 옅어졌다. 층을 대표하는 맵 하나씩이면 숲 → 사막 → 바다가 또렷하다.
    /// </summary>
    private static readonly string[] Sources =
    {
        "Assets/Game/Prefabs/Rooms/scene1.prefab",
        "Assets/Game/Prefabs/Rooms/scene2.prefab",
        "Assets/Game/Prefabs/Rooms/scene3.prefab",
    };

    /// <summary>찍는 크기. 가로는 화면(16:9)보다 넓어야 밀 자리가 생긴다.</summary>
    private const int Width = 720;
    private const int Height = 270;

    /// <summary>게임 카메라와 같은 크기. 유닛당 24픽셀이 되는 값이다.</summary>
    private const float OrthoSize = 5.625f;

    /// <summary>맵 하나를 세울 자리. 씬에 이미 있는 것들과 겹치지 않게 멀리 떨어뜨린다.</summary>
    private static readonly Vector3 Stage = new Vector3(0f, 500f, 0f);

    [MenuItem("Game/타이틀 배경 맵 스틸 굽기")]
    public static void BakeAllMenu() => Debug.Log(BakeAll());

    public static string BakeAll()
    {
        // ⚠️ 재생 중에 구우면 안 된다. 프리팹을 세우는 순간 Awake가 돌아 머리 위 체력바가
        // 생기는데, 그 프레임 안에 맵을 지워 버리므로 Start가 하는 크기 맞추기를 못 받는다.
        // 채움 조각이 기본 크기(1x1 유닛)로 남아 적마다 <b>붉은 사각형</b>이 하나씩 박혔다.
        // 한 번 겪었고, 그림만 보고는 무엇인지 알아내기 어려웠다.
        if (EditorApplication.isPlaying)
            return "재생을 멈추고 다시 구울 것 — 재생 중에는 적마다 붉은 사각형(체력바 조각)이 함께 찍힌다";

        Directory.CreateDirectory(Folder);
        RemoveOldStills();

        var camGo = new GameObject("__TitleMapCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = OrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // 맵 그림 밖으로는 나가지 않지만, 혹시 비면 어두운 색이 보이는 편이 흰색보다 낫다.
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        cam.cullingMask = ~0;
        cam.enabled = false;            // 씬 화면에 끼어들지 않게 하고 필요할 때만 Render한다

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point;
        cam.targetTexture = rt;

        var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        int done = 0;
        string missing = "";

        try
        {
            for (int i = 0; i < Sources.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Sources[i]);
                if (prefab == null) { missing += " " + Sources[i]; continue; }

                var map = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (map == null) continue;

                // 프리팹이 들고 있는 자리가 곧 맵 중심이다. 그 상대 배치를 흐트리지 않으려면
                // 자리를 옮기는 대신 카메라를 그 위로 보낸다.
                Vector3 center = map.transform.position;
                map.transform.position = center + Stage;
                camGo.transform.position = center + Stage + new Vector3(0f, 0f, -10f);

                HidePlaceholders(map);
                cam.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(Folder + FileNameFor(done) + ".png", shot.EncodeToPNG());
                Object.DestroyImmediate(map);
                done++;
            }
        }
        finally
        {
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        AssetDatabase.Refresh();
        Import(done);
        return "타이틀 배경 " + done + "장 구움 (" + Width + "x" + Height + ")"
             + (missing.Length > 0 ? "  없는 프리팹:" + missing : "");
    }

    /// <summary>
    /// 지난번에 구운 스틸을 먼저 지운다. 장 수가 줄어들면(스물한 장 → 세 장) 남은 파일이
    /// 그대로 <c>Resources.LoadAll</c>에 걸려, 지워진 방들이 배경에 계속 흐른다.
    /// </summary>
    private static void RemoveOldStills()
    {
        foreach (string path in Directory.GetFiles(Folder, "TitleMap*.png"))
            AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
    }

    /// <summary>
    /// 재생 중에만 제 모습을 갖추는 것들을 감춘다.
    ///
    /// 에디터에서는 <c>Awake</c>가 돌지 않으므로, 프리팹에 굳어 있는 <b>자리표시자</b>가 그대로
    /// 찍힌다. 게임에서는 아무도 보지 못하는 것들이라 배경에 남으면 얼룩으로 보인다.
    ///
    /// <list type="bullet">
    /// <item><b>상점 상품</b> — 판마다 새로 뽑히므로 프리팹에는 흰 사각형으로 남아 있다.</item>
    /// <item><b>출구 문</b> — 오른쪽 통로에 놓인 짙은 갈색 판이다. 통로를 막는 일은 이제 구름이
    ///   하고(<c>RoomGates</c>) <see cref="ExitDoor"/>는 <c>Awake</c>에서 자기 그림을 끄는데,
    ///   그 코드가 돌지 않으니 갈색 블록이 남는다.</item>
    /// </list>
    /// </summary>
    private static void HidePlaceholders(GameObject map)
    {
        foreach (ShopItem item in map.GetComponentsInChildren<ShopItem>(true))
            item.gameObject.SetActive(false);

        foreach (ExitDoor door in map.GetComponentsInChildren<ExitDoor>(true))
            door.gameObject.SetActive(false);
    }

    /// <summary>이름에 번호를 두 자리로 박아 둔다. <see cref="TitleMapBackdrop"/>이 이 순서로 읽는다.</summary>
    public static string FileNameFor(int index) => "TitleMap" + index.ToString("00");

    private static void Import(int count)
    {
        for (int i = 0; i < count; i++)
        {
            string path = Folder + FileNameFor(i) + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // 화면에서 정수배로 늘리므로 PPU는 캔버스 기준(100)에 맞춰 두고 크기는 코드가 정한다.
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
