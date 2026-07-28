using UnityEngine;

/// <summary>
/// 갸라도스 전투장의 상시 기믹 — 삼중 해류.
///
/// 위·가운데·아래 세 수로가 각각 독립적으로 왼쪽 또는 오른쪽으로 흐른다. 해류 자체는 피해가 없고
/// 플레이어의 X축 이동에만 힘을 더한다. 갸라도스·잉어킹·예고 표시는 영향을 받지 않는다.
///
/// 힘을 더하는 방식이 중요하다. <see cref="PlayerController"/>는 매 <c>FixedUpdate</c>마다
/// 입력으로 속도를 통째로 다시 쓰므로, 여기서 속도를 먼저 바꿔 봐야 그대로 덮어써진다.
/// 그래서 이 컴포넌트는 <see cref="DefaultExecutionOrder"/>로 플레이어보다 늦게 돌면서
/// <b>이미 계산된 입력 속도에 외부 속도를 더한다</b>. 트랜스폼을 직접 옮기지 않으므로
/// 벽과 잉어킹 충돌은 물리가 그대로 처리한다.
///
/// 범람에 맞았을 때의 이동 감속도 같은 자리에서 처리한다. <c>PlayerController.SpeedMultiplier</c>는
/// <see cref="PlayerCombat"/>가 매 프레임 덮어쓰는 값이라 밖에서 건드리면 안 되고,
/// 어차피 "플레이어에게 걸리는 외부 힘"이라는 점에서 해류와 같은 종류의 값이다.
/// </summary>
[DefaultExecutionOrder(200)]
public class WaterCurrentField : MonoBehaviour
{
    /// <summary>수로 번호. 아래(0) → 가운데(1) → 위(2) 순으로 Y가 커진다.</summary>
    public const int LaneBottom = 0;
    public const int LaneMiddle = 1;
    public const int LaneTop = 2;
    public const int LaneCount = 3;

    /// <summary>지형(-10, -5)보다 위, 예고(0)보다 아래. 화살표는 모든 예고 밑에 깔린다.</summary>
    private const int ArrowSortingOrder = -1;

    private Vector2 center;
    private Vector2 halfSize;

    private float speed;
    private float minHoldTime;
    private float maxHoldTime;
    private float telegraphTime;

    private readonly int[] signs = { 1, -1, 1 };
    private readonly int[] pendingSigns = { 1, -1, 1 };

    private bool running;
    private float nextChangeTime;
    private float telegraphStartTime;
    private float telegraphEndTime;

    private Transform player;
    private Rigidbody2D playerBody;

    private float slowMultiplier = 1f;
    private float slowUntil = -999f;

    // 연출
    private Transform arrowRoot;
    private SpriteRenderer[][] arrows;
    private float arrowSpacing;
    private float arrowScrollSpeed;
    private Color arrowColor;
    private Color boundaryColor;
    /// <summary>수로마다 쌓인 화살표 흐름 거리. 방향이 도는 동안에도 튀지 않게 누적으로 관리한다.</summary>
    private readonly float[] scrollOffset = new float[LaneCount];

    /// <summary>방향 변경을 예고하는 중인지. 화살표 점멸 연출이 이 값을 본다.</summary>
    public bool IsChanging { get; private set; }

    /// <summary>지금 세 수로의 방향. 인덱스는 <see cref="LaneBottom"/> 계열 상수를 쓴다.</summary>
    public int SignOf(int lane) => signs[Mathf.Clamp(lane, 0, LaneCount - 1)];

    /// <summary>세 수로의 방향을 `우좌우` 같은 한 줄로. 로그용.</summary>
    public string SignText() => Text(signs);

    private static string Text(int[] source) =>
        Label(source[LaneTop]) + Label(source[LaneMiddle]) + Label(source[LaneBottom]);

    private static string Label(int sign) => sign < 0 ? "좌" : "우";

    /// <summary>수로 경계의 Y값. <paramref name="index"/> 0이 아래쪽 경계, 1이 위쪽 경계다.</summary>
    public float BoundaryY(int index) =>
        center.y + (index == 0 ? -halfSize.y / 3f : halfSize.y / 3f);

    /// <summary>
    /// 그 수로의 한가운데 높이. 화살표를 여기에 띄운다 — 경계선에 붙어 그리면 어느 수로의
    /// 방향인지 헷갈린다. 아래 수로는 [-halfSize.y, -halfSize.y/3], 가운데는 그 사이,
    /// 위 수로는 [halfSize.y/3, halfSize.y]이므로 중심은 각각 ∓(halfSize.y*2/3)과 0이다.
    /// </summary>
    public float LaneCenterY(int lane) =>
        center.y + (Mathf.Clamp(lane, 0, LaneCount - 1) - 1) * (halfSize.y * 2f / 3f);

    /// <summary>이 Y가 들어 있는 수로. 전투 영역 밖이면 가장 가까운 수로로 본다.</summary>
    public int LaneAt(float y)
    {
        if (y < BoundaryY(0)) return LaneBottom;
        if (y > BoundaryY(1)) return LaneTop;
        return LaneMiddle;
    }

    /// <summary>전투장 크기와 연출 값을 받는다. 보스 컨트롤러가 한 번만 호출한다.</summary>
    public void Configure(Vector2 arenaCenter, Vector2 arenaHalfSize, int arrowsPerLane,
                          float scrollSpeed, Color arrowTint, Color boundaryTint)
    {
        center = arenaCenter;
        halfSize = arenaHalfSize;
        arrowScrollSpeed = scrollSpeed;
        arrowColor = arrowTint;
        boundaryColor = boundaryTint;
        BuildVisuals(Mathf.Max(2, arrowsPerLane));
    }

    /// <summary>페이즈별 수치. 전환할 때 다시 호출한다.</summary>
    public void SetTuning(float currentSpeed, float minHold, float maxHold, float telegraph)
    {
        speed = Mathf.Max(0f, currentSpeed);
        minHoldTime = Mathf.Max(0.1f, minHold);
        maxHoldTime = Mathf.Max(minHoldTime, maxHold);
        telegraphTime = Mathf.Max(0.05f, telegraph);
    }

    /// <summary>해류를 켠다. Intro가 끝나는 순간부터 흐르기 시작한다.</summary>
    public void Begin()
    {
        if (running) return;
        running = true;
        RollSigns(signs);
        System.Array.Copy(signs, pendingSigns, LaneCount);
        ScheduleNextChange();
        ApplyArrowDirections(signs);
        SetVisualsVisible(true);
    }

    /// <summary>보스 사망·방 이탈에서만 부른다. 페이즈 전환은 해류를 멈추지 않는다.</summary>
    public void StopField()
    {
        running = false;
        IsChanging = false;
        SetVisualsVisible(false);
    }

    /// <summary>페이즈 전환에서 예고 없이 즉시 새 방향을 뽑는다.</summary>
    public void ForceChangeNow()
    {
        if (!running) return;
        IsChanging = false;
        RollSigns(signs);
        System.Array.Copy(signs, pendingSigns, LaneCount);
        ApplyArrowDirections(signs);
        ScheduleNextChange();
    }

    /// <summary>
    /// 범람에 맞았을 때의 이동 감속. 중첩하지 않고 지속시간만 새로 채운다.
    /// 해류와 같은 자리에서 곱하므로 플레이어의 다른 속도 배율을 건드리지 않는다.
    /// </summary>
    public void ApplyPlayerSlow(float multiplier, float duration)
    {
        if (duration <= 0f) return;
        slowMultiplier = Mathf.Clamp01(multiplier);
        slowUntil = Time.time + duration;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        player = pc.transform;
        playerBody = pc.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!running) return;

        if (IsChanging)
        {
            if (Time.time >= telegraphEndTime)
            {
                // 예고가 끝나는 한 프레임에 세 수로를 함께 확정한다.
                System.Array.Copy(pendingSigns, signs, LaneCount);
                IsChanging = false;
                ApplyArrowDirections(signs);
                ScheduleNextChange();
                Changed?.Invoke();
            }
        }
        else if (Time.time >= nextChangeTime)
        {
            BeginChange();
        }

        AnimateArrows();
    }

    /// <summary>방향이 확정된 순간. 보스 컨트롤러가 로그를 남길 때 쓴다.</summary>
    public event System.Action Changed;

    /// <summary>방향 변경을 예고하기 직전의 조합. 로그에서 이전/이후를 함께 보여 준다.</summary>
    public string PreviousSignText { get; private set; } = "";
    /// <summary>예고 중인 새 조합.</summary>
    public string PendingSignText => Text(pendingSigns);

    private void BeginChange()
    {
        PreviousSignText = SignText();
        RollSigns(pendingSigns, signs);
        IsChanging = true;
        telegraphStartTime = Time.time;
        telegraphEndTime = Time.time + telegraphTime;
        ChangeTelegraphStarted?.Invoke();
    }

    /// <summary>변경 예고가 시작된 순간.</summary>
    public event System.Action ChangeTelegraphStarted;

    private void ScheduleNextChange() =>
        nextChangeTime = Time.time + Random.Range(minHoldTime, maxHoldTime);

    /// <summary>
    /// 세 수로의 방향을 각각 독립적인 50%로 뽑는다. 여덟 조합을 모두 허용하되,
    /// <paramref name="previous"/>와 완전히 같은 조합은 다시 뽑는다. 몇 번을 뽑아도 같으면
    /// 무한 반복하지 않고 임의의 한 수로를 뒤집어 최소 한 곳은 바뀌게 한다.
    /// </summary>
    private static void RollSigns(int[] into, int[] previous = null)
    {
        const int MaxRolls = 8;
        for (int attempt = 0; attempt < MaxRolls; attempt++)
        {
            for (int i = 0; i < LaneCount; i++) into[i] = Random.value < 0.5f ? -1 : 1;
            if (previous == null || !Same(into, previous)) return;
        }
        int flip = Random.Range(0, LaneCount);
        into[flip] = -into[flip];
    }

    private static bool Same(int[] a, int[] b)
    {
        for (int i = 0; i < LaneCount; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    // ---------------------------------------------------------------- 플레이어에게 걸리는 힘

    /// <summary>
    /// 플레이어보다 늦게 도는 <c>FixedUpdate</c>. 입력으로 이미 정해진 속도에 감속과 해류를 더한다.
    /// 여기서 속도를 새로 쓰지 않고 <b>더하기만</b> 하므로 원래 이동 규칙은 그대로 남는다.
    /// </summary>
    private void FixedUpdate()
    {
        if (playerBody == null) return;

        Vector2 velocity = playerBody.linearVelocity;
        bool changed = false;

        if (Time.time < slowUntil)
        {
            velocity *= slowMultiplier;
            changed = true;
        }

        if (running && speed > 0f && player != null)
        {
            // 플레이어 중심이 들어 있는 수로의 해류 하나만 적용한다.
            velocity += Vector2.right * (signs[LaneAt(player.position.y)] * speed);
            changed = true;
        }

        if (changed) playerBody.linearVelocity = velocity;
    }

    // ---------------------------------------------------------------- 연출

    /// <summary>수로마다 반복 화살표를, 경계마다 옅은 거품선을 만든다.</summary>
    private void BuildVisuals(int arrowsPerLane)
    {
        if (arrowRoot != null) Destroy(arrowRoot.gameObject);

        arrowRoot = new GameObject("CurrentArrows").transform;
        arrowRoot.SetParent(transform, false);

        float laneHeight = halfSize.y * 2f / 3f;
        float usableWidth = halfSize.x * 2f - laneHeight * 0.5f;
        arrowSpacing = usableWidth / arrowsPerLane;

        arrows = new SpriteRenderer[LaneCount][];
        for (int lane = 0; lane < LaneCount; lane++)
        {
            arrows[lane] = new SpriteRenderer[arrowsPerLane];
            for (int i = 0; i < arrowsPerLane; i++)
            {
                GameObject go = new GameObject("Arrow");
                go.transform.SetParent(arrowRoot, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PrimitiveSprites.Triangle;
                sr.color = arrowColor;
                sr.sortingOrder = ArrowSortingOrder;
                go.transform.localScale = new Vector3(laneHeight * 0.42f, laneHeight * 0.34f, 1f);
                arrows[lane][i] = sr;
            }
        }

        // 수로 경계는 바닥을 새로 만들지 않고 옅은 거품선으로만 나눈다.
        for (int i = 0; i < 2; i++)
        {
            GameObject go = new GameObject("LaneBoundary");
            go.transform.SetParent(arrowRoot, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprites.Square;
            sr.color = boundaryColor;
            sr.sortingOrder = ArrowSortingOrder;
            go.transform.position = new Vector3(center.x, BoundaryY(i), 0f);
            go.transform.localScale = new Vector3(halfSize.x * 2f, 0.07f, 1f);
        }

        SetVisualsVisible(false);
    }

    private void SetVisualsVisible(bool visible)
    {
        if (arrowRoot != null) arrowRoot.gameObject.SetActive(visible);
    }

    private void ApplyArrowDirections(int[] source)
    {
        if (arrows == null) return;
        for (int lane = 0; lane < LaneCount; lane++)
            for (int i = 0; i < arrows[lane].Length; i++)
                if (arrows[lane][i] != null)
                    arrows[lane][i].transform.rotation =
                        Quaternion.Euler(0f, 0f, source[lane] < 0 ? 180f : 0f);
    }

    /// <summary>
    /// 화살표를 흐르는 방향으로 흘려보낸다.
    ///
    /// 방향 변경을 예고하는 동안에는 예전 방향과 새 방향을 <b>번갈아 보여 주지 않는다.</b> 그렇게
    /// 하면 화살표가 초당 몇 번씩 홱홱 뒤집혀, 어느 쪽으로 바뀌는지 읽히기는커녕 화면만 어지럽다.
    /// 대신 예고 시간에 걸쳐 <b>천천히 돌린다</b> — 도는 것이 곧 예고이고, 다 돌아간 방향이 곧
    /// 새 방향이다. 흐르는 속도도 같은 비율로 줄었다가 반대로 붙으므로, 화살표가 서서히 멈추고
    /// 뒤돌아 다시 흐르는 모습이 된다.
    /// </summary>
    private void AnimateArrows()
    {
        if (arrows == null) return;

        // 예고 진행도. 0이면 아직 예전 방향, 1이면 완전히 새 방향이다.
        float turn = 0f;
        if (IsChanging && telegraphEndTime > telegraphStartTime)
            turn = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Time.time - telegraphStartTime) / (telegraphEndTime - telegraphStartTime)));

        // 도는 동안에는 옅게 숨 쉬어 "지금 바뀌는 중"임을 한 번 더 알린다. 깜빡임이 아니라
        // 완만한 호흡이라 눈에 거슬리지 않는다.
        float alpha = arrowColor.a * (IsChanging ? 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 4f)) : 1f);

        for (int lane = 0; lane < LaneCount; lane++)
        {
            // 수로 한가운데 높이. 경계가 center.y ± halfSize.y/3이므로 각 수로의 높이는
            // 모두 같고, 중심은 아래·가운데·위가 각각 -laneHeight, 0, +laneHeight다.
            float laneY = LaneCenterY(lane);
            SpriteRenderer[] row = arrows[lane];

            // 이 수로가 실제로 방향을 바꾸는 경우에만 돌린다. 그대로인 수로는 흔들리지 않는다.
            float from = signs[lane];
            float to = IsChanging ? pendingSigns[lane] : signs[lane];
            float flow = Mathf.Lerp(from, to, turn);
            float angle = Mathf.Lerp(from < 0f ? 180f : 0f, to < 0f ? 180f : 360f, turn);
            if (Mathf.Approximately(from, to)) angle = from < 0f ? 180f : 0f;

            // 흐름이 눈에 보이도록 계속 밀어 준다. 부호가 도중에 뒤집히므로 시각(Time.time)에
            // 곱하지 않고 프레임마다 누적한다 — 곱했다가는 뒤집히는 순간 위치가 통째로 튄다.
            float span = arrowSpacing * row.Length;
            scrollOffset[lane] += Time.deltaTime * arrowScrollSpeed * flow;
            scrollOffset[lane] = Mathf.Repeat(scrollOffset[lane], span);

            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == null) continue;
                float x = center.x - halfSize.x +
                          Mathf.Repeat(i * arrowSpacing + arrowSpacing * 0.5f + scrollOffset[lane], span);
                row[i].transform.position = new Vector3(x, laneY, 0f);
                row[i].transform.rotation = rotation;
                Color c = arrowColor;
                c.a = alpha;
                row[i].color = c;
            }
        }
    }
}
