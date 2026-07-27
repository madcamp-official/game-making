using UnityEngine;

/// <summary>
/// 개발용 치트 패널. 우측 하단에 버튼 몇 개를 띄워 방 이동과 진화 단계를 바로 바꾼다.
///
/// 임시 도구다. 이 파일 하나만 지우면 흔적 없이 사라지도록,
/// 씬이나 프리팹에 붙이지 않고 실행 시작할 때 스스로 자기 오브젝트를 만든다.
/// (같이 지울 것: <see cref="RoomFlowController.WarpTo"/>, <see cref="PlayerEvolution.SetStageImmediate"/>)
///
/// 라벨은 영문이다. IMGUI 기본 폰트에 한글 글리프가 없어서 한글로 쓰면 빈칸으로 나온다.
/// </summary>
public class DevHackPanel : MonoBehaviour
{
    private const float PanelWidth = 132f;
    private const float Margin = 8f;
    private const float OpenHeight = 262f;

    private bool open = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        GameObject go = new GameObject("DevHackPanel");
        go.AddComponent<DevHackPanel>();
        DontDestroyOnLoad(go);
    }

    private void OnGUI()
    {
        float height = open ? OpenHeight : 26f;
        Rect area = new Rect(Screen.width - PanelWidth - Margin,
                             Screen.height - height - Margin, PanelWidth, height);

        GUI.Box(area, GUIContent.none);
        GUILayout.BeginArea(area);

        if (GUILayout.Button(open ? "DEV HACK  -" : "DEV HACK  +")) open = !open;
        if (open)
        {
            RoomFlowController flow = FindAnyObjectByType<RoomFlowController>();
            if (GUILayout.Button("1F Boss")) Warp(flow, 0, -1);
            if (GUILayout.Button("2F Start")) Warp(flow, 1, 0);
            if (GUILayout.Button("2F Boss")) Warp(flow, 1, -1);
            if (GUILayout.Button("3F Start")) Warp(flow, 2, 0);
            if (GUILayout.Button("3F Boss")) Warp(flow, 2, -1);

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
        }

        GUILayout.EndArea();
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
