using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 타이틀 배경으로 흐를 <b>방 스틸</b>을 굽는다. 스물한 방을 하나씩 세워 카메라로 찍어
/// <c>Resources/UI/TitleMaps/</c>에 넣어 두고, <see cref="TitleMapBackdrop"/>이 그것을 가로로 흘린다.
///
/// 왜 실시간으로 방을 띄우지 않는가: 방 프리팹에는 적과 그 능력 스크립트가 함께 들어 있다.
/// 타이틀 뒤에서 그것들을 돌리면 적이 돌아다니고 장판을 깔고 탄을 쏜다 — 게다가 플레이어를
/// 찾아 헤맨다. <b>판이 언제 시작됐는지가 흐려진다</b>는 이유로 <see cref="RoomFlowController"/>가
/// 일부러 타이틀 동안 방을 올리지 않는데, 배경으로 되살리면 그 결정을 뒷문으로 무르는 셈이다.
/// 그림 한 장으로 굽고 나면 타이틀은 아무것도 계산하지 않는다.
///
/// 에디터에서 굽는 것이 요점이다. 재생 모드가 아니면 <c>Awake</c>·<c>Start</c>가 돌지 않아
/// 프리팹은 <b>배치된 그대로</b> 찍힌다 — 적은 첫 프레임 자세로 서 있고 아무 스크립트도 돌지 않는다.
///
/// 해상도는 <c>480x270</c>이고 카메라 크기는 게임 카메라와 같은 <c>5.625</c>다. 그래야
/// 유닛당 24픽셀이 되어 <b>타일 한 칸이 정확히 24픽셀</b>로 찍히고, 화면에서 정수배(4배)로
/// 늘리면 원본 픽셀 격자가 그대로 살아난다. 여기서 어긋나면 배경만 흐릿해진다.
/// </summary>
public static class TitleMapSetup
{
    private const string Folder = "Assets/Game/Resources/UI/TitleMaps/";
    private const string FloorFolder = "Assets/Game/Data/Floors";

    /// <summary>찍는 크기. 4배로 늘려 1920x1080을 정확히 채운다.</summary>
    private const int Width = 480;
    private const int Height = 270;

    /// <summary>게임 카메라와 같은 크기. 유닛당 24픽셀이 되는 값이다.</summary>
    private const float OrthoSize = 5.625f;

    /// <summary>방 하나를 세울 자리. 씬에 이미 있는 것들과 겹치지 않게 멀리 떨어뜨린다.</summary>
    private static readonly Vector3 Stage = new Vector3(0f, 500f, 0f);

    [MenuItem("Game/타이틀 배경 방 스틸 굽기")]
    public static void BakeAllMenu() => Debug.Log(BakeAll());

    public static string BakeAll()
    {
        // ⚠️ 재생 중에 구우면 안 된다. 프리팹을 세우는 순간 Awake가 돌아 머리 위 체력바가
        // 생기는데, 그 프레임 안에 방을 지워 버리므로 Start가 하는 크기 맞추기를 못 받는다.
        // 채움 조각이 기본 크기(1x1 유닛)로 남아 적마다 <b>붉은 사각형</b>이 하나씩 박혔다.
        // 한 번 겪었고, 그림만 보고는 무엇인지 알아내기 어려웠다.
        if (EditorApplication.isPlaying)
            return "재생을 멈추고 다시 구울 것 — 재생 중에는 적마다 붉은 사각형(체력바 조각)이 함께 찍힌다";

        List<GameObject> prefabs = CollectRoomPrefabs();
        if (prefabs.Count == 0) return "방 프리팹을 찾지 못했다 — " + FloorFolder + "의 FloorData를 확인할 것";

        Directory.CreateDirectory(Folder);

        var camGo = new GameObject("__TitleMapCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = OrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // 방 그림 밖으로는 나가지 않지만, 혹시 비면 어두운 색이 보이는 편이 흰색보다 낫다.
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        cam.cullingMask = ~0;
        cam.enabled = false;            // 씬 화면에 끼어들지 않게 하고 필요할 때만 Render한다

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point;
        cam.targetTexture = rt;

        var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        int done = 0;

        try
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
                if (room == null) continue;

                // 프리팹이 들고 있는 자리가 곧 방 중심이다 (RoomFlowController도 그대로 세운다).
                // 그 상대 배치를 흐트리지 않으려면 자리를 옮기는 대신 카메라를 그 위로 보낸다.
                Vector3 center = room.transform.position;
                room.transform.position = center + Stage;
                camGo.transform.position = center + Stage + new Vector3(0f, 0f, -10f);

                HidePlaceholders(room);
                cam.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(Folder + FileNameFor(i) + ".png", shot.EncodeToPNG());
                Object.DestroyImmediate(room);
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
        return "타이틀 배경 " + done + "장 구움 (" + Width + "x" + Height + ")";
    }

    /// <summary>
    /// 판이 시작될 때 채워지는 자리표시자를 감춘다.
    ///
    /// 상점 상품은 판마다 새로 뽑히므로 프리팹에는 <b>흰 사각형</b>으로 남아 있다. 재생 중에는
    /// 곧바로 그림이 들어가지만 에디터에서 찍으면 그대로 찍혀, 상점 방 스틸에 흰 상자 셋이
    /// 박힌다 — 배경에 난 구멍처럼 보인다. 진열대는 그대로 두고 상품만 뺀다.
    /// </summary>
    private static void HidePlaceholders(GameObject room)
    {
        foreach (ShopItem item in room.GetComponentsInChildren<ShopItem>(true))
            item.gameObject.SetActive(false);
    }

    /// <summary>
    /// 층 순서대로 방 프리팹을 모은다. <see cref="RoomFlowController"/>의 목록은 비공개라
    /// 층 데이터 에셋에서 직접 읽는다 — 씬이 열려 있지 않아도 굽을 수 있다는 이점이 따라온다.
    /// </summary>
    private static List<GameObject> CollectRoomPrefabs()
    {
        var result = new List<GameObject>();
        string[] guids = AssetDatabase.FindAssets("t:FloorData", new[] { FloorFolder });
        var floors = new List<FloorData>();
        foreach (string guid in guids)
        {
            var floor = AssetDatabase.LoadAssetAtPath<FloorData>(AssetDatabase.GUIDToAssetPath(guid));
            if (floor != null) floors.Add(floor);
        }
        // 파일 이름이 Floor1·Floor2·Floor3이라 이름 순이 곧 층 순서다.
        floors.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        foreach (FloorData floor in floors)
        {
            if (floor.roomPrefabs == null) continue;
            foreach (GameObject prefab in floor.roomPrefabs)
                if (prefab != null) result.Add(prefab);
        }
        return result;
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
            // 화면에서 4배로 늘리므로 PPU는 캔버스 기준(100)에 맞춰 두고 크기는 코드가 정한다.
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
