using UnityEngine;

/// <summary>
/// 개발용 치트 패널. 우측 하단에서 층·방을 골라 바로 이동하고 진화 단계를 바꾼다.
///
/// 층을 접었다 펴는 폴더 방식이다. 방이 층당 7개라 전부 펼치면 화면을 덮어 버린다.
///
/// 임시 도구다. 이 파일 하나만 지우면 흔적 없이 사라지도록,
/// 씬이나 프리팹에 붙이지 않고 실행 시작할 때 스스로 자기 오브젝트를 만든다.
/// (같이 지울 것: <see cref="RoomFlowController"/>의 개발용 멤버들,
///  <see cref="PlayerEvolution.SetStageImmediate"/>)
///
/// 라벨은 영문이다. IMGUI 기본 폰트에 한글 글리프가 없어서 한글로 쓰면 빈칸으로 나온다.
/// </summary>
public class DevHackPanel : MonoBehaviour
{
    private const float PanelWidth = 150f;
    private const float Margin = 8f;
    private const float RowHeight = 21f;
    private const float ClosedHeight = 26f;

    private bool open = true;
    /// <summary>펼쳐 둔 층. -1이면 전부 접혀 있다. 한 번에 하나만 펼친다.</summary>
    private int expandedFloor = -1;
    private Vector2 scroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        GameObject go = new GameObject("DevHackPanel");
        go.AddComponent<DevHackPanel>();
        DontDestroyOnLoad(go);
    }

    private void OnGUI()
    {
        RoomFlowController flow = FindAnyObjectByType<RoomFlowController>();
        int floorCount = flow != null ? flow.FloorCount : 0;

        float height = open ? MeasureHeight(flow, floorCount) : ClosedHeight;
        Rect area = new Rect(Screen.width - PanelWidth - Margin,
                             Screen.height - height - Margin, PanelWidth, height);

        GUI.Box(area, GUIContent.none);
        GUILayout.BeginArea(area);

        if (GUILayout.Button(open ? "DEV HACK  -" : "DEV HACK  +")) open = !open;
        if (open)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            DrawFloors(flow, floorCount);

            GUILayout.Space(6f);

            PlayerEvolution evolution = FindAnyObjectByType<PlayerEvolution>();
            if (GUILayout.Button("Bulbasaur")) SetStage(evolution, 0);
            if (GUILayout.Button("Ivysaur")) SetStage(evolution, 1);
            if (GUILayout.Button("Venusaur")) SetStage(evolution, 2);

            GUILayout.Space(6f);

            // 유물 등장 순서에서 다음 유물을 바로 받는다.
            RelicManager relics = RelicManager.Instance;
            int left = relics != null ? relics.RemainingCount : 0;
            if (GUILayout.Button("Relic +1  (" + left + ")") && relics != null)
                RelicManager.GrantReward(null);

            GUILayout.EndScrollView();
        }

        GUILayout.EndArea();
    }

    /// <summary>층 목록. 펼친 층만 방 버튼을 늘어놓는다.</summary>
    private void DrawFloors(RoomFlowController flow, int floorCount)
    {
        for (int floor = 0; floor < floorCount; floor++)
        {
            bool expanded = expandedFloor == floor;
            if (GUILayout.Button((expanded ? "v " : "> ") + (floor + 1) + "F"))
                expandedFloor = expanded ? -1 : floor;
            if (!expanded) continue;

            int rooms = flow.RoomCount(floor);
            for (int room = 0; room < rooms; room++)
            {
                // "3  Event"처럼 번호와 종류를 같이 보여 준다.
                string label = "   " + (room + 1) + "  " + flow.RoomKindLabel(floor, room);
                if (GUILayout.Button(label)) Warp(flow, floor, room);
            }
        }
    }

    /// <summary>펼친 층까지 포함한 실제 높이. 화면을 넘기면 스크롤이 받아 준다.</summary>
    private float MeasureHeight(RoomFlowController flow, int floorCount)
    {
        int rows = 1 + floorCount + 3 + 1;   // 제목 + 층 + 진화 3개 + 유물
        if (expandedFloor >= 0 && flow != null) rows += flow.RoomCount(expandedFloor);
        float wanted = rows * RowHeight + 24f;
        return Mathf.Min(wanted, Screen.height - Margin * 2f);
    }

    private static void Warp(RoomFlowController flow, int floor, int room)
    {
        if (flow == null) return;
        // 시작 화면이 켜져 있으면 게임이 멈춰 있으니 같이 풀어 준다.
        Time.timeScale = 1f;
        flow.WarpTo(floor, room);
    }

    private static void SetStage(PlayerEvolution evolution, int stage)
    {
        if (evolution != null) evolution.SetStageImmediate(stage);
    }
}
