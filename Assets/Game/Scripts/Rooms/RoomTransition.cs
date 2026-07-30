using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방과 방 사이를 잇는 연출. 출구에 닿으면 화면을 어둡게 덮고, 다음 방을 올린 뒤
/// 다시 밝히면서 주인공이 왼쪽 통로에서 방 안으로 걸어 들어오게 한다. 방에 들어서면
/// 왼쪽 통로에 구름이 차 되돌아갈 수 없게 된다.
///
/// 화면을 덮는 이유: 방을 갈아 끼우는 순간은 옛 방이 사라지고 새 방이 생기는 한 프레임이라,
/// 그대로 보이면 세상이 깜빡 바뀐다. 어둠으로 덮어 두면 그 이음매가 보이지 않고,
/// 밝아지며 걸어 들어오는 동안 <b>새 방을 눈으로 훑을 시간</b>이 생긴다.
///
/// UI는 코드로 만든다. 이 프로젝트의 다른 화면(<c>GameStartScreen</c>, <c>UIManager</c>)과
/// 같은 방식이라 씬이나 프리팹을 건드릴 필요가 없다.
/// </summary>
public class RoomTransition : MonoBehaviour
{
    public static RoomTransition Instance { get; private set; }

    [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.3f;
    [SerializeField, Min(0.05f)] private float fadeInDuration = 0.35f;
    [Tooltip("걸어 들어오기 시작하는 자리. 방 중심에서 왼쪽으로 이만큼 떨어진 통로다.")]
    [SerializeField] private float entryX = -10.5f;
    [Tooltip("걸어 들어와 멈추는 자리. 방 안쪽이라 왼쪽 구름이 등 뒤에서 닫힌다.")]
    [SerializeField] private float arriveX = -5.5f;
    [Tooltip("걸어 들어오는 데 걸리는 시간의 상한. 벽에 걸려도 여기서 끊고 조작을 돌려준다.")]
    [SerializeField, Min(0.5f)] private float walkTimeout = 4f;

    /// <summary>지금 연출이 도는 중인지. 출구를 두 번 밟는 것을 막는다.</summary>
    public bool IsPlaying { get; private set; }

    private Image veil;

    /// <summary>
    /// 아직 없으면 만들어 돌려준다. 씬에 심어 두지 않아도 첫 방 이동에서 저절로 생긴다.
    /// </summary>
    public static RoomTransition Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("RoomTransition");
        DontDestroyOnLoad(go);
        return go.AddComponent<RoomTransition>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildVeil();
    }

    /// <summary>출구에 닿았다. 다음 방으로 넘어가는 연출을 시작한다.</summary>
    public void Go()
    {
        if (IsPlaying) return;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        IsPlaying = true;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.ControlEnabled = false;

        yield return Fade(0f, 1f, fadeOutDuration);

        // 어둠 아래에서 방을 갈아 끼운다.
        if (RoomFlowController.Instance != null) RoomFlowController.Instance.NextRoom();
        // 방을 올리면서 RoomFlowController가 주인공을 제 자리에 놓는다. 한 프레임 기다려
        // 새 방의 구름(RoomGates)이 생기고 나서 손을 댄다.
        yield return null;

        // 게임 클리어처럼 다음 방이 없는 경우엔 걸어 들어올 방이 없다. 밝히고 끝낸다.
        RoomGates gates = RoomGates.Current;
        if (player == null || gates == null)
        {
            yield return Fade(1f, 0f, fadeInDuration);
            if (player != null) player.ControlEnabled = true;
            IsPlaying = false;
            yield break;
        }

        // 통로 밖에 세우고 왼쪽 구름을 열어 둔다.
        Vector2 center = gates.transform.parent != null
            ? (Vector2)gates.transform.parent.position : Vector2.zero;
        player.transform.position = new Vector3(center.x + entryX, center.y, 0f);
        gates.Left.SetOpenImmediate(true);

        yield return Fade(1f, 0f, fadeInDuration);

        // 걸어 들어온다. 조작은 아직 꺼져 있고, 방향만 넣어 준다.
        float deadline = Time.time + walkTimeout;
        player.SetScriptedMove(Vector2.right);
        while (player.transform.position.x < center.x + arriveX && Time.time < deadline)
            yield return null;
        player.ClearScriptedMove();

        // 등 뒤에서 길이 막힌다.
        gates.Left.Close();

        // 다 들어와 멈춘 지금이 "방에 들어섰다"이다. 보스방이라면 여기서 울음소리가 난다 —
        // 방을 올리는 순간에 울리면 아직 화면이 검고 주인공은 통로 밖에 있다.
        if (RoomFlowController.Instance != null) RoomFlowController.Instance.OnPlayerEnteredRoom();

        player.ControlEnabled = true;
        IsPlaying = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (veil == null) yield break;
        float elapsed = 0f;
        SetVeil(from);
        while (elapsed < duration)
        {
            // 이벤트 대사창 등이 timeScale을 0으로 세워 둔 채 방을 넘어갈 수 있다.
            // 실제 시간으로 재야 화면이 검은 채로 멈추지 않는다.
            elapsed += Time.unscaledDeltaTime;
            SetVeil(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetVeil(to);
    }

    private void SetVeil(float alpha)
    {
        Color c = veil.color;
        c.a = Mathf.Clamp01(alpha);
        veil.color = c;
        // 다 밝아지면 클릭을 가로채지 않도록 아예 끈다.
        veil.enabled = c.a > 0.001f;
    }

    private void BuildVeil()
    {
        var canvasGo = new GameObject("TransitionCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // HUD보다 위에 덮어야 방 이름·골드까지 함께 어두워진다.
        canvas.sortingOrder = 500;
        canvasGo.AddComponent<CanvasScaler>();

        var veilGo = new GameObject("Veil");
        veilGo.transform.SetParent(canvasGo.transform, false);
        veil = veilGo.AddComponent<Image>();
        veil.color = new Color(0f, 0f, 0f, 0f);
        veil.raycastTarget = false;
        RectTransform rt = veil.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        veil.enabled = false;
    }
}
