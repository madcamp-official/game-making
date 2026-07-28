using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2층 보스 코뿌리의 전투 로직.
///
/// 버터플과 정반대의 보스다. 버터플은 거리를 벌리고 탄을 뿌리지만, 코뿌리는 투사체를 하나도
/// 쓰지 않는다. 세 패턴 모두 몸이 닿는 거리에서만 위험하고, 그래서 압박은 "피해라"가 아니라
/// "어디에 서 있을 것이냐"로 온다.
///
/// * 스톤샤워 — 자기 주변에 갈색 원을 깔고 그 안에 돌을 떨어뜨린다. 원 안이 곧 자기 몸 주변이라,
///   때리려면 원 안에 있어야 하고 원 안에 있으면 맞는다.
/// * 뿔드릴 — 짧게 차지한 뒤 거대한 삼각뿔을 창처럼 찌른다.
/// * 이판사판 — 방 반대편까지 연속으로 돌진한다.
///
/// 체력이 절반이 되면 버터플과 같은 방식으로 2페이즈에 들어간다. 새 패턴은 없고 같은 셋의 강화형만 쓴다.
///
/// <see cref="EnemyController"/>의 기본 추적 AI와 Rigidbody를 동시에 조작하면 안 되므로,
/// 이 컴포넌트가 켜질 때 기본 AI를 끈다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody2D))]
public class RhydonBossController : MonoBehaviour
{
    private enum Pattern { StoneShower, HornDrill, TakeDown }

    // ---------------------------------------------------------------- 패턴 설정

    [System.Serializable]
    private class StoneSettings
    {
        [Tooltip("돌이 떨어지는 갈색 원의 반지름. 코뿌리 자신을 중심으로 깔린다.")]
        public float radius = 3.2f;
        [Tooltip("원이 깔리고 첫 돌이 떨어지기까지의 시간")]
        public float windup = 0.7f;
        [Tooltip("떨어뜨릴 돌 수")]
        public int stoneCount = 9;
        [Tooltip("돌을 하나씩 떨구는 간격")]
        public float spawnInterval = 0.24f;
        [Tooltip("그림자가 생기고 돌이 바닥에 닿기까지의 시간. 짧을수록 반응하기 어렵다.")]
        public float fallTime = 0.7f;
        [Tooltip("마지막 돌이 떨어진 뒤 후딜레이")]
        public float recovery = 0.75f;
    }

    [System.Serializable]
    private class HornSettings
    {
        [Tooltip("찌르기 전 차지 시간")]
        public float chargeTime = 0.75f;
        [Tooltip("뿔의 길이. 이만큼 앞까지 닿는다.")]
        public float length = 3.4f;
        [Tooltip("뿔 밑변의 너비. 끝으로 갈수록 좁아진다.")]
        public float baseWidth = 1.7f;
        [Tooltip("뿔이 다 뻗어 나오는 데 걸리는 시간")]
        public float thrustTime = 0.15f;
        [Tooltip("다 뻗은 채로 머무는 시간")]
        public float holdTime = 0.1f;
        [Tooltip("뿔이 도로 들어가는 시간")]
        public float retractTime = 0.14f;
        [Tooltip("한 번의 패턴에서 찌르는 횟수. 찌를 때마다 다시 조준한다.")]
        public int stabs = 2;
        [Tooltip("다음 찌르기의 차지를 시작하기까지의 간격")]
        public float stabGap = 0.28f;
        [Tooltip("찌르면서 앞으로 밀고 나가는 거리")]
        public float lunge = 0.9f;
        public float recovery = 0.8f;
    }

    [System.Serializable]
    private class TakeDownSettings
    {
        [Tooltip("연속으로 돌진하는 횟수")]
        public int dashes = 3;
        [Tooltip("돌진 한 번의 조준 시간")]
        public float aimTime = 0.42f;
        public float dashSpeed = 15f;
        [Tooltip("벽에 끼어도 패턴이 무한히 늘어지지 않게 하는 상한")]
        public float dashMaxDuration = 1.3f;
        [Tooltip("돌진이 끝나고 다음 조준을 시작하기까지의 간격")]
        public float betweenDashes = 0.2f;
        public float recovery = 0.9f;
    }

    // 빠른 전투에서도 읽을 수 있어야 하는 절대 하한. Inspector 값이 더 작아도 이 아래로는 내려가지 않는다.
    private const float MinCharge = 0.3f;
    private const float MinFallTime = 0.35f;
    private const float MinAimTime = 0.28f;

    [Header("기본")]
    [Tooltip("걸어다닐 때의 속도. 플레이어는 5다. 코뿌리는 느린 대신 돌진이 빠르다.")]
    [SerializeField, Min(0f)] private float moveSpeed = 3.4f;
    [SerializeField, Min(0f)] private float introDelay = 0.15f;
    [Tooltip("전투 영역의 중심. 비워 두면 부모(방)의 위치를 쓴다.")]
    [SerializeField] private Transform arenaCenter;
    [Tooltip("전투 영역의 반너비·반높이. 돌과 돌진 목표를 이 안으로 제한한다.")]
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(6.2f, 4.2f);

    [Header("접근")]
    [Tooltip("스톤샤워를 깔기 전에 이만큼까지 붙는다. 원이 자기 주변이라 붙어야 의미가 있다.")]
    [SerializeField, Min(0f)] private float stoneApproachDistance = 1.6f;
    [Tooltip("뿔드릴을 쓰기 전에 이만큼까지 붙는다. 뿔 길이보다 짧아야 한다.")]
    [SerializeField, Min(0f)] private float hornApproachDistance = 2.4f;
    [Tooltip("한 번의 접근에 쓸 수 있는 최대 시간. 벽에 막혀도 패턴이 무한히 밀리지 않게 한다.")]
    [SerializeField, Min(0f)] private float approachMaxDuration = 1.6f;

    [Header("패턴 사이 대기")]
    [SerializeField, Min(0f)] private float patternGapPhase1 = 0.6f;
    [SerializeField, Min(0f)] private float patternGapPhase2 = 0.45f;

    [Header("스톤샤워 — 1페이즈")]
    [SerializeField] private StoneSettings stonePhase1 = new StoneSettings
    {
        radius = 3.2f, windup = 0.7f, stoneCount = 9,
        spawnInterval = 0.24f, fallTime = 0.7f, recovery = 0.75f,
    };

    // 2페이즈: 원의 "넓이"를 50% 늘리므로 반지름은 √1.5 = 1.2247배다 (3.2 → 3.92).
    // 떨어지는 속도는 30% 빨라지므로 낙하 시간은 1/1.3배가 된다 (0.7 → 0.538).
    [Header("스톤샤워 — 2페이즈")]
    [SerializeField] private StoneSettings stonePhase2 = new StoneSettings
    {
        radius = 3.92f, windup = 0.6f, stoneCount = 13,
        spawnInterval = 0.2f, fallTime = 0.538f, recovery = 0.6f,
    };

    [Header("스톤샤워 — 공통")]
    [SerializeField, Min(0)] private int stoneDamage = 22;
    [Tooltip("돌 하나가 때리는 반지름")]
    [SerializeField, Min(0f)] private float stoneRadius = 0.62f;
    [Tooltip("착탄 판정이 남아 있는 시간. 짧게 두어 지나간 자리는 곧 안전해진다.")]
    [SerializeField, Min(0f)] private float stoneImpactDuration = 0.22f;
    [Tooltip("돌이 화면 위 어디에서부터 떨어지는지")]
    [SerializeField, Min(0f)] private float stoneDropHeight = 6f;
    [Tooltip("이 비율만큼은 플레이어의 현재 위치를 노린다. 나머지는 원 안 무작위. " +
             "전부 무작위면 원 안에서 가만히 서 있는 게 통해 버린다.")]
    [SerializeField, Range(0f, 1f)] private float stoneAimAtPlayerRatio = 0.4f;

    [Header("뿔드릴 — 1페이즈")]
    [SerializeField] private HornSettings hornPhase1 = new HornSettings
    {
        chargeTime = 0.75f, length = 3.4f, baseWidth = 1.7f,
        thrustTime = 0.15f, holdTime = 0.1f, retractTime = 0.14f,
        stabs = 2, stabGap = 0.28f, lunge = 0.9f, recovery = 0.8f,
    };

    // 2페이즈: 뿔이 커지고 차지가 짧아진다.
    [Header("뿔드릴 — 2페이즈")]
    [SerializeField] private HornSettings hornPhase2 = new HornSettings
    {
        chargeTime = 0.42f, length = 4.5f, baseWidth = 2.4f,
        thrustTime = 0.13f, holdTime = 0.1f, retractTime = 0.12f,
        stabs = 2, stabGap = 0.22f, lunge = 1.2f, recovery = 0.65f,
    };

    [Header("뿔드릴 — 공통")]
    [SerializeField, Min(0)] private int hornDamage = 26;

    [Header("이판사판 — 1페이즈")]
    [SerializeField] private TakeDownSettings takeDownPhase1 = new TakeDownSettings
    {
        dashes = 3, aimTime = 0.42f, dashSpeed = 15f,
        dashMaxDuration = 1.3f, betweenDashes = 0.2f, recovery = 0.9f,
    };

    // 2페이즈: 속도 30% 증가(15 → 19.5), 돌진 3회 → 5회.
    [Header("이판사판 — 2페이즈")]
    [SerializeField] private TakeDownSettings takeDownPhase2 = new TakeDownSettings
    {
        dashes = 5, aimTime = 0.34f, dashSpeed = 19.5f,
        dashMaxDuration = 1.2f, betweenDashes = 0.16f, recovery = 0.7f,
    };

    [Header("이판사판 — 공통")]
    [SerializeField, Min(0)] private int dashDamage = 24;
    [Tooltip("돌진 중 이 거리 안에 있으면 들이받힌다.")]
    [SerializeField, Min(0f)] private float dashHitRadius = 1.05f;
    [Tooltip("돌진이 도착점에 이만큼 가까워지면 끝난 것으로 본다.")]
    [SerializeField, Min(0f)] private float dashStopDistance = 0.4f;

    [Header("페이즈 전환")]
    [Tooltip("2페이즈에 들어간 뒤 공격하지 않고 두는 시간. 전환을 인지할 여유를 준다.")]
    [SerializeField, Min(0f)] private float phase2GraceTime = 0.6f;

    [Header("접촉 피해")]
    [SerializeField, Min(0)] private int contactDamage = 14;
    [SerializeField, Min(0f)] private float contactInterval = 1f;

    [Header("디버그")]
    [Tooltip("패턴 선택과 진행을 콘솔에 남긴다. 수치를 조정할 때만 켠다.")]
    [SerializeField] private bool logPatterns;

    [Header("연출 색상")]
    [Tooltip("스톤샤워 원의 안쪽")]
    [SerializeField] private Color stoneAreaColor = new Color(0.55f, 0.36f, 0.18f, 0.2f);
    [Tooltip("스톤샤워 원의 테두리")]
    [SerializeField] private Color stoneEdgeColor = new Color(0.72f, 0.47f, 0.22f, 0.85f);
    [Tooltip("돌이 떨어질 자리 그림자")]
    [SerializeField] private Color stoneShadowColor = new Color(0.1f, 0.07f, 0.04f, 0.45f);
    [Tooltip("떨어지는 돌 자체")]
    [SerializeField] private Color stoneColor = new Color(0.42f, 0.3f, 0.19f, 1f);
    [Tooltip("착탄 순간")]
    [SerializeField] private Color stoneImpactColor = new Color(0.85f, 0.55f, 0.25f, 0.8f);
    // 2층 바닥은 밝은 모래색이고 코뿌리는 회색이다. 노란 예고와 흰 뿔은 둘 다 묻혀서 안 보였다.
    // 버터플이 쓰던 규칙(위험 = 진한 빨강)을 그대로 따르고, 뿔 자체는 어두운 강철색으로 둔다.
    [SerializeField] private Color hornWarningColor = new Color(0.85f, 0.1f, 0.28f, 0.62f);
    [SerializeField] private Color hornColor = new Color(0.22f, 0.24f, 0.3f, 1f);
    [SerializeField] private Color dashWarningColor = new Color(0.85f, 0.1f, 0.28f, 0.5f);
    [SerializeField] private Color windupTint = new Color(1f, 0.72f, 0.62f, 1f);

    /// <summary>공중의 돌과 뿔은 캐릭터(10)보다 앞에 그린다.</summary>
    private const int AirborneSortingOrder = 12;
    private const float DashTelegraphWidth = 1.6f;

    private bool inPhase2;
    private bool phaseTransitionPending;
    private bool phaseTransitionDone;
    private bool phaseInvulnerabilityActive;

    /// <summary>지금 프레임에 몸을 붙들어 둘지. 예고·후딜에서 밀려나지 않게 한다.</summary>
    private bool holdPosition = true;
    /// <summary>돌진 중에는 접촉 피해 대신 더 아픈 돌진 피해를 쓴다.</summary>
    private bool dashing;

    private EnemyController enemyController;
    private EnemyAnimator enemyAnimator;
    private Health health;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Health playerHealth;
    private Vector2 fallbackArenaCenter;
    private float nextContactDamageTime;

    /// <summary>보스가 만든 모든 오브젝트의 부모. 사망·방 이동 시 통째로 지운다.</summary>
    private Transform attackRoot;
    /// <summary>아직 떨어지는 중인 돌. 페이즈 전환에서 한 번에 지운다.</summary>
    private readonly List<GameObject> fallingStones = new List<GameObject>();
    /// <summary>페이즈 전환 등으로 공격을 정리한 뒤, 예약된 착탄이 되살아나지 못하게 한다.</summary>
    private int attackGeneration;

    // 셔플 백. 세 패턴을 한 번씩 다 쓰기 전에는 같은 패턴이 다시 나오지 않는다.
    private readonly List<Pattern> bag = new List<Pattern>(3);
    private bool hasLastPattern;
    private Pattern lastPattern;

    private Vector2 ArenaCenter => arenaCenter != null ? (Vector2)arenaCenter.position : fallbackArenaCenter;
    private Vector2 PlayerPosition => player != null ? (Vector2)player.position : ArenaCenter;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        fallbackArenaCenter = transform.parent != null
            ? (Vector2)transform.parent.position : (Vector2)transform.position;

        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;
    }

    private void OnEnable()
    {
        // 기본 추적 AI와 이 컨트롤러가 동시에 Rigidbody를 만지면 안 된다.
        enemyController.SetBasicAIEnabled(false);
    }

    private void OnDisable()
    {
        EndPhaseInvulnerability();
        // 파괴 중이면 EnemyController가 이미 사라졌을 수 있다.
        if (enemyController != null) enemyController.SetBasicAIEnabled(true);
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
        }

        // 배율 1인 씬 루트에 둔다. 보스(1.2배) 아래에 두면 돌과 뿔까지 커진다.
        attackRoot = new GameObject("Rhydon_Attacks").transform;

        StartCoroutine(Battle());
    }

    private void OnDestroy()
    {
        EndPhaseInvulnerability();
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
        // 방을 넘어갈 때 공격 오브젝트가 씬에 남으면 안 된다.
        // 씬 자체가 내려가는 중이면 정리할 필요가 없다.
        if (attackRoot != null && gameObject.scene.isLoaded) Destroy(attackRoot.gameObject);
    }

    /// <summary>예고·후딜에서는 절대 움직이지 않는다. 플레이어와 부딪혀 밀리는 것도 막는다.</summary>
    private void Update()
    {
        if (holdPosition) body.linearVelocity = Vector2.zero;
    }

    private void HandleDamaged()
    {
        if (phaseTransitionPending || phaseTransitionDone) return;
        if (health.CurrentHealth > health.MaxHealth * 0.5f) return;
        // 실행 중인 패턴은 정상적으로 끝낸 뒤 전환한다. 여기서는 요청만 기록한다.
        phaseTransitionPending = true;
    }

    private void HandleDied()
    {
        EndPhaseInvulnerability();
        StopAllCoroutines();
        holdPosition = true;
        dashing = false;
        body.linearVelocity = Vector2.zero;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (enemyAnimator != null) enemyAnimator.ClearActionState();
        // 예고, 떨어지던 돌, 착탄 판정을 전부 지워 사망 후 추가 피해가 없게 한다.
        ClearAttackObjects();
        if (attackRoot != null) Destroy(attackRoot.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // 돌진 중에는 돌진 피해가 따로 들어간다. 여기서 또 때리면 두 배로 아프다.
        if (dashing || health.IsDead || contactDamage <= 0) return;
        if (Time.time < nextContactDamageTime) return;
        if (collision.collider.GetComponentInParent<PlayerController>() == null) return;
        if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return;

        playerHealth.TakeDamage(contactDamage);
        nextContactDamageTime = Time.time + contactInterval;
    }

    // ---------------------------------------------------------------- 전투 흐름

    private IEnumerator Battle()
    {
        holdPosition = true;
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(introDelay);

        // 방에 들어서자마자 달려든다. 첫 패턴은 대기 없이 시작하고 간격은 두 번째부터 적용한다.
        bool firstPattern = true;

        while (!health.IsDead)
        {
            if (phaseTransitionPending && !phaseTransitionDone)
            {
                phaseTransitionDone = true;
                yield return PhaseTransitionRoutine();
                firstPattern = false;
                continue;
            }

            holdPosition = true;
            if (!firstPattern)
                yield return new WaitForSeconds(inPhase2 ? patternGapPhase2 : patternGapPhase1);
            firstPattern = false;
            if (health.IsDead) yield break;

            Pattern next = DrawPattern();
            lastPattern = next;
            hasLastPattern = true;
            Trace((inPhase2 ? "P2 " : "P1 ") + next + " 시작 (남은 백 " + bag.Count + ")");

            switch (next)
            {
                case Pattern.StoneShower:
                    yield return ApproachRoutine(stoneApproachDistance);
                    if (health.IsDead) yield break;
                    yield return StoneShowerRoutine();
                    break;
                case Pattern.HornDrill:
                    yield return ApproachRoutine(hornApproachDistance);
                    if (health.IsDead) yield break;
                    yield return HornDrillRoutine();
                    break;
                case Pattern.TakeDown:
                    // 돌진은 어차피 방 반대편까지 가므로 미리 붙지 않는다.
                    yield return TakeDownRoutine();
                    break;
            }
        }
    }

    /// <summary>
    /// 셔플 백에서 다음 패턴을 꺼낸다. 백이 비면 세 패턴을 다시 채워 섞고,
    /// 이때 직전에 쓴 패턴이 맨 앞이면 뒤로 밀어 같은 패턴이 연달아 나오지 않게 한다.
    /// </summary>
    private Pattern DrawPattern()
    {
        if (bag.Count == 0) RefillBag();

        int index = bag.Count - 1;
        Pattern next = bag[index];
        bag.RemoveAt(index);
        return next;
    }

    private void RefillBag()
    {
        bag.Clear();
        bag.Add(Pattern.StoneShower);
        bag.Add(Pattern.HornDrill);
        bag.Add(Pattern.TakeDown);

        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        // 꺼내는 쪽이 리스트의 끝이므로, 마지막 칸이 직전 패턴이면 앞쪽과 바꾼다.
        int last = bag.Count - 1;
        if (hasLastPattern && bag[last] == lastPattern)
            (bag[last], bag[0]) = (bag[0], bag[last]);
    }

    /// <summary>페이즈 전환·사망에서 예고와 떨어지던 돌, 남은 판정을 한 번에 없앤다.</summary>
    private void ClearAttackObjects()
    {
        attackGeneration++;

        for (int i = fallingStones.Count - 1; i >= 0; i--)
            if (fallingStones[i] != null) Destroy(fallingStones[i]);
        fallingStones.Clear();

        if (attackRoot == null) return;
        foreach (AttackTelegraph telegraph in attackRoot.GetComponentsInChildren<AttackTelegraph>(true))
            Destroy(telegraph.gameObject);
        foreach (DamageZone zone in attackRoot.GetComponentsInChildren<DamageZone>(true))
            Destroy(zone.gameObject);
    }

    // ---------------------------------------------------------------- 이동

    /// <summary>플레이어에게 걸어서 붙는다. 목표 거리에 닿거나 시간이 다하면 끝난다.</summary>
    private IEnumerator ApproachRoutine(float targetDistance)
    {
        holdPosition = false;
        float deadline = Time.time + approachMaxDuration;

        while (Time.time < deadline && !health.IsDead && player != null)
        {
            Vector2 self = transform.position;
            Vector2 inward = InwardPush(self);
            if (inward != Vector2.zero)
            {
                body.linearVelocity = inward * moveSpeed;
                yield return null;
                continue;
            }

            Vector2 toPlayer = (Vector2)player.position - self;
            if (toPlayer.magnitude <= targetDistance) break;
            body.linearVelocity = toPlayer.normalized * moveSpeed;
            yield return null;
        }

        holdPosition = true;
        body.linearVelocity = Vector2.zero;
    }

    /// <summary>벽에 몰렸으면 전투 영역 안쪽으로 향하는 방향, 아니면 0.</summary>
    private Vector2 InwardPush(Vector2 position)
    {
        Vector2 offset = position - ArenaCenter;
        Vector2 push = Vector2.zero;
        if (Mathf.Abs(offset.x) > arenaHalfSize.x) push.x = -Mathf.Sign(offset.x);
        if (Mathf.Abs(offset.y) > arenaHalfSize.y) push.y = -Mathf.Sign(offset.y);
        return push == Vector2.zero ? Vector2.zero : push.normalized;
    }

    private Vector2 ClampToArena(Vector2 position, float margin)
    {
        Vector2 center = ArenaCenter;
        return new Vector2(
            Mathf.Clamp(position.x, center.x - arenaHalfSize.x + margin, center.x + arenaHalfSize.x - margin),
            Mathf.Clamp(position.y, center.y - arenaHalfSize.y + margin, center.y + arenaHalfSize.y - margin));
    }

    private Vector2 DirectionTo(Vector2 target)
    {
        Vector2 delta = target - (Vector2)transform.position;
        return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
    }

    // ---------------------------------------------------------------- 패턴 1 · 스톤샤워

    /// <summary>
    /// 코뿌리를 중심으로 갈색 원을 깔고, 그 안에 돌을 하나씩 떨어뜨린다.
    /// 원이 곧 코뿌리 주변이라 때리려면 원 안에 서야 하고, 원 안에 있으면 맞는다 —
    /// 이 패턴만큼은 공격을 포기하고 나가는 게 정답이다.
    /// </summary>
    private IEnumerator StoneShowerRoutine()
    {
        StoneSettings settings = inPhase2 ? stonePhase2 : stonePhase1;
        float fallTime = Mathf.Max(MinFallTime, settings.fallTime);

        holdPosition = true;
        body.linearVelocity = Vector2.zero;
        SetWindupTint(true);
        FaceTowardPlayer();

        // 원은 시전 시작 시점의 자리에 고정한다. 코뿌리는 이 패턴 동안 움직이지 않는다.
        Vector2 center = transform.position;
        int stones = Mathf.Max(1, settings.stoneCount);
        float showerTime = settings.windup + (stones - 1) * settings.spawnInterval + fallTime;

        AttackTelegraph area = AttackTelegraph.CreateCircle(attackRoot, center, settings.radius, stoneAreaColor);
        area.Hold(showerTime);
        // 테두리가 있어야 "여기서부터 안전"이 한눈에 읽힌다.
        AttackTelegraph edge = AttackTelegraph.CreateRing(attackRoot, center, settings.radius, stoneEdgeColor);
        edge.Hold(showerTime);

        yield return new WaitForSeconds(settings.windup);
        SetWindupTint(false);
        // 시전 방향 고정을 풀지 않으면 패턴이 끝난 뒤에도 그 방향으로 굳은 채 걷는다.
        if (enemyAnimator != null) enemyAnimator.ClearActionState();
        if (health.IsDead) yield break;

        for (int i = 0; i < stones; i++)
        {
            if (health.IsDead) yield break;
            StartCoroutine(FallingStoneRoutine(NextStoneTarget(center, settings), fallTime, attackGeneration));
            if (i < stones - 1) yield return new WaitForSeconds(settings.spawnInterval);
        }

        Trace(string.Format("  스톤샤워: 반지름 {0:0.00}, {1}개, 낙하 {2:0.00}초", settings.radius, stones, fallTime));

        // 마지막 돌이 떨어질 때까지 기다린 뒤 후딜레이. 안 그러면 후딜 중에 돌이 떨어진다.
        yield return new WaitForSeconds(fallTime);
        yield return new WaitForSeconds(settings.recovery);
    }

    /// <summary>돌 하나가 떨어질 자리. 일부는 플레이어를 직접 노려 제자리 버티기를 막는다.</summary>
    private Vector2 NextStoneTarget(Vector2 center, StoneSettings settings)
    {
        Vector2 raw;
        if (Random.value < stoneAimAtPlayerRatio && player != null)
        {
            // 원 밖으로 도망친 뒤라면 노려도 소용없다. 원 안으로 당겨 둔다.
            Vector2 toPlayer = PlayerPosition - center;
            raw = center + Vector2.ClampMagnitude(toPlayer, settings.radius);
        }
        else
        {
            // 반지름에 √를 씌워야 원 안에 고르게 흩어진다. 안 그러면 중심에 몰린다.
            float angle = Random.value * 360f;
            float distance = settings.radius * Mathf.Sqrt(Random.value);
            raw = center + Rotate(Vector2.right, angle) * distance;
        }
        return ClampToArena(raw, stoneRadius);
    }

    /// <summary>그림자가 먼저 뜨고, 돌이 하늘에서 떨어져 착탄한다.</summary>
    private IEnumerator FallingStoneRoutine(Vector2 target, float fallTime, int generation)
    {
        AttackTelegraph shadow = AttackTelegraph.CreateCircle(attackRoot, target, stoneRadius, stoneShadowColor);
        shadow.Hold(fallTime);

        GameObject stone = new GameObject("FallingStone");
        stone.transform.SetParent(attackRoot, false);
        SpriteRenderer sr = stone.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Circle;
        sr.color = stoneColor;
        sr.sortingOrder = AirborneSortingOrder;
        stone.transform.localScale = Vector3.one * (stoneRadius * 2f);
        fallingStones.Add(stone);

        float elapsed = 0f;
        while (elapsed < fallTime)
        {
            // 페이즈 전환이 돌을 치웠으면 여기서 끝낸다.
            if (generation != attackGeneration || stone == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallTime);
            // t를 제곱해 떨어질수록 빨라지게 한다. 등속으로 내려오면 무게가 안 느껴진다.
            stone.transform.position = target + Vector2.up * Mathf.Lerp(stoneDropHeight, 0f, t * t);
            yield return null;
        }

        fallingStones.Remove(stone);
        if (stone != null) Destroy(stone);
        if (generation != attackGeneration || health.IsDead) yield break;

        // 착탄 판정. 무적 시간 처리는 DamageZone이 맡는다.
        DamageZone.Spawn(attackRoot, target, stoneRadius, stoneImpactDuration,
                         stoneDamage, 1f, stoneImpactColor);
    }

    // ---------------------------------------------------------------- 패턴 2 · 뿔드릴

    /// <summary>
    /// 짧게 차지한 뒤 거대한 삼각뿔을 창처럼 찌른다. 예고 삼각형과 실제 판정은 같은 모양이다.
    /// 찌를 때마다 다시 조준하므로, 첫 찌르기를 피한 방향으로 그대로 서 있으면 두 번째에 맞는다.
    /// </summary>
    private IEnumerator HornDrillRoutine()
    {
        HornSettings settings = inPhase2 ? hornPhase2 : hornPhase1;
        int stabs = Mathf.Max(1, settings.stabs);

        for (int s = 0; s < stabs; s++)
        {
            if (health.IsDead) yield break;

            // 조준은 차지 시작 시점에 확정하고, 차지 중에는 갱신하지 않는다.
            Vector2 aim = DirectionTo(PlayerPosition);
            float charge = Mathf.Max(MinCharge, settings.chargeTime);

            holdPosition = true;
            body.linearVelocity = Vector2.zero;
            SetWindupTint(true);
            if (enemyAnimator != null) enemyAnimator.SetActionState("Idle", aim);

            AttackTelegraph warning = AttackTelegraph.CreateTriangle(
                attackRoot, transform.position, aim, settings.length, settings.baseWidth, hornWarningColor);
            warning.Pulse(charge);

            yield return new WaitForSeconds(charge);
            SetWindupTint(false);
            if (health.IsDead) yield break;

            yield return ThrustRoutine(aim, settings);
            if (enemyAnimator != null) enemyAnimator.ClearActionState();
            if (health.IsDead) yield break;

            if (s < stabs - 1)
            {
                holdPosition = true;
                yield return new WaitForSeconds(settings.stabGap);
            }
        }

        Trace(string.Format("  뿔드릴: {0}회, 길이 {1:0.00}, 차지 {2:0.00}초",
            stabs, settings.length, Mathf.Max(MinCharge, settings.chargeTime)));

        holdPosition = true;
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(settings.recovery);
    }

    /// <summary>뿔이 뻗었다 들어간다. 뻗어 나온 길이만큼만 판정이 있다.</summary>
    private IEnumerator ThrustRoutine(Vector2 aim, HornSettings settings)
    {
        GameObject horn = new GameObject("Horn");
        horn.transform.SetParent(attackRoot, false);
        SpriteRenderer sr = horn.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Triangle;
        sr.color = hornColor;
        sr.sortingOrder = AirborneSortingOrder;
        horn.transform.rotation = Quaternion.FromToRotation(Vector3.right, aim);

        // 한 번의 찌르기에 한 번만 맞는다.
        bool struck = false;
        // 몸을 앞으로 밀며 찌른다. 창처럼 짧은 거리를 밀고 들어가는 느낌을 준다.
        float lungeSpeed = settings.thrustTime > 0f ? settings.lunge / settings.thrustTime : 0f;

        // 뻗기
        holdPosition = false;
        float elapsed = 0f;
        while (elapsed < settings.thrustTime)
        {
            elapsed += Time.deltaTime;
            body.linearVelocity = aim * lungeSpeed;
            float grown = settings.length * Mathf.Clamp01(elapsed / Mathf.Max(0.01f, settings.thrustTime));
            PlaceHorn(horn, aim, grown, settings.baseWidth);
            if (!struck && TryHornHit(aim, grown, settings.baseWidth)) struck = true;
            yield return null;
        }

        holdPosition = true;
        body.linearVelocity = Vector2.zero;

        // 다 뻗은 채로 유지
        elapsed = 0f;
        while (elapsed < settings.holdTime)
        {
            elapsed += Time.deltaTime;
            PlaceHorn(horn, aim, settings.length, settings.baseWidth);
            if (!struck && TryHornHit(aim, settings.length, settings.baseWidth)) struck = true;
            yield return null;
        }

        // 들어가기. 줄어드는 동안에도 남은 길이만큼은 아직 위험하다.
        elapsed = 0f;
        while (elapsed < settings.retractTime)
        {
            elapsed += Time.deltaTime;
            float shrunk = settings.length * (1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, settings.retractTime)));
            PlaceHorn(horn, aim, shrunk, settings.baseWidth);
            if (!struck && TryHornHit(aim, shrunk, settings.baseWidth)) struck = true;
            yield return null;
        }

        if (horn != null) Destroy(horn);
    }

    /// <summary>뿔의 밑변을 몸에 붙이고 꼭짓점을 앞으로 둔다.</summary>
    private void PlaceHorn(GameObject horn, Vector2 aim, float length, float baseWidth)
    {
        if (horn == null) return;
        horn.transform.position = (Vector2)transform.position + aim * (length * 0.5f);
        horn.transform.localScale = new Vector3(length, baseWidth, 1f);
    }

    /// <summary>
    /// 플레이어가 지금 뻗어 있는 삼각뿔 안에 있는지. 끝으로 갈수록 좁아지는 모양을 그대로 판정한다 —
    /// 그린 것보다 넓게 맞으면 예고가 거짓말이 된다.
    /// </summary>
    private bool TryHornHit(Vector2 aim, float length, float baseWidth)
    {
        if (length <= 0.01f || player == null) return false;
        if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return false;

        Vector2 offset = (Vector2)player.position - (Vector2)transform.position;
        float along = Vector2.Dot(offset, aim);
        if (along < 0f || along > length) return false;

        float side = Mathf.Abs(Vector2.Dot(offset, new Vector2(-aim.y, aim.x)));
        float halfWidthHere = baseWidth * 0.5f * (1f - along / length);
        if (side > halfWidthHere) return false;

        playerHealth.TakeDamage(hornDamage);
        return true;
    }

    // ---------------------------------------------------------------- 패턴 3 · 이판사판

    /// <summary>
    /// 조준한 방향으로 방 반대편까지 그대로 밀고 나간다. 연속으로 여러 번 하며,
    /// 돌진마다 다시 조준하므로 한 번 피한 자리에 그대로 서 있으면 다음 돌진에 받힌다.
    /// </summary>
    private IEnumerator TakeDownRoutine()
    {
        TakeDownSettings settings = inPhase2 ? takeDownPhase2 : takeDownPhase1;
        int dashes = Mathf.Max(1, settings.dashes);

        for (int d = 0; d < dashes; d++)
        {
            if (health.IsDead) yield break;

            Vector2 aim = DirectionTo(PlayerPosition);
            Vector2 target = ArenaEdgePoint(transform.position, aim);
            float aimTime = Mathf.Max(MinAimTime, settings.aimTime);

            holdPosition = true;
            body.linearVelocity = Vector2.zero;
            SetWindupTint(true);
            if (enemyAnimator != null) enemyAnimator.SetActionState("Idle", aim);

            float length = Vector2.Distance(transform.position, target);
            AttackTelegraph line = AttackTelegraph.CreateLine(
                attackRoot, transform.position, aim, length, DashTelegraphWidth, dashWarningColor);
            line.Pulse(aimTime);

            yield return new WaitForSeconds(aimTime);
            SetWindupTint(false);
            if (enemyAnimator != null) enemyAnimator.ClearActionState();
            if (health.IsDead) yield break;

            yield return DashRoutine(target, settings);
            if (health.IsDead) yield break;

            if (d < dashes - 1)
            {
                holdPosition = true;
                body.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(settings.betweenDashes);
            }
        }

        Trace(string.Format("  이판사판: {0}회, 속도 {1:0.0}", dashes, settings.dashSpeed));

        holdPosition = true;
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(settings.recovery);
    }

    private IEnumerator DashRoutine(Vector2 target, TakeDownSettings settings)
    {
        holdPosition = false;
        dashing = true;
        float deadline = Time.time + settings.dashMaxDuration;
        // 돌진 방향은 시작할 때 고정한다. 매 프레임 목표 쪽으로 다시 겨누면, 프레임이 길어
        // 한 번에 목표를 지나친 순간 뒤로 돌아 제자리에서 앞뒤로 튀게 된다.
        Vector2 heading = (target - (Vector2)transform.position).normalized;
        if (heading == Vector2.zero) heading = Vector2.right;

        while (Time.time < deadline && !health.IsDead)
        {
            Vector2 toTarget = target - (Vector2)transform.position;
            // 도착했거나 이미 지나쳤으면 끝.
            if (toTarget.magnitude <= dashStopDistance || Vector2.Dot(toTarget, heading) <= 0f) break;
            body.linearVelocity = heading * settings.dashSpeed;
            TryDashHit();
            yield return null;
        }

        dashing = false;
        holdPosition = true;
        body.linearVelocity = Vector2.zero;
    }

    /// <summary>돌진 중 플레이어를 들이받았는지. 무적 시간이 연타를 막아 준다.</summary>
    private void TryDashHit()
    {
        if (player == null || playerHealth == null) return;
        if (playerHealth.IsDead || playerHealth.IsInvincible) return;
        if (Vector2.Distance(player.position, transform.position) > dashHitRadius) return;

        playerHealth.TakeDamage(dashDamage);
    }

    /// <summary>주어진 방향으로 전투 영역 경계까지 갔을 때의 지점.</summary>
    private Vector2 ArenaEdgePoint(Vector2 origin, Vector2 direction)
    {
        Vector2 center = ArenaCenter;
        Vector2 offset = origin - center;
        // 경계에 닿기까지 갈 수 있는 거리를 축별로 구해 더 짧은 쪽을 쓴다.
        float distance = Mathf.Max(arenaHalfSize.x, arenaHalfSize.y) * 2f;
        if (Mathf.Abs(direction.x) > 0.0001f)
        {
            float limit = (Mathf.Sign(direction.x) * arenaHalfSize.x - offset.x) / direction.x;
            if (limit > 0f) distance = Mathf.Min(distance, limit);
        }
        if (Mathf.Abs(direction.y) > 0.0001f)
        {
            float limit = (Mathf.Sign(direction.y) * arenaHalfSize.y - offset.y) / direction.y;
            if (limit > 0f) distance = Mathf.Min(distance, limit);
        }
        return origin + direction * Mathf.Max(0.5f, distance);
    }

    // ---------------------------------------------------------------- 페이즈 전환

    private IEnumerator PhaseTransitionRoutine()
    {
        holdPosition = true;
        dashing = false;
        body.linearVelocity = Vector2.zero;
        BeginPhaseInvulnerability();
        // 전환 연출 중에는 새 공격 판정이 남아 있으면 안 된다.
        ClearAttackObjects();
        if (enemyAnimator != null) enemyAnimator.ClearActionState();

        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < 1f && !health.IsDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed);
            transform.localScale = baseScale * Mathf.Lerp(1f, 1.18f, t);
            if (spriteRenderer != null)
                spriteRenderer.color = Color.Lerp(Color.white, stoneEdgeColor, t);
            yield return null;
        }
        transform.localScale = baseScale;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (health.IsDead) yield break;

        // 피해 없는 충격파
        AttackTelegraph wave = AttackTelegraph.CreateRing(
            attackRoot, transform.position, 0.6f, stoneEdgeColor);
        wave.Expand(0.6f, 6f, 0.6f);
        yield return new WaitForSeconds(0.6f);
        if (health.IsDead) yield break;

        inPhase2 = true;
        Trace("2페이즈 전환 — 남은 공격 오브젝트 정리, 셔플 백 초기화");
        // 강화 패턴을 새 셔플 백으로 다시 뽑는다.
        bag.Clear();
        hasLastPattern = false;

        yield return new WaitForSeconds(phase2GraceTime);
        EndPhaseInvulnerability();
    }

    private void BeginPhaseInvulnerability()
    {
        if (phaseInvulnerabilityActive || health == null || health.IsDead) return;
        health.BeginInvulnerability();
        phaseInvulnerabilityActive = true;
    }

    private void EndPhaseInvulnerability()
    {
        if (!phaseInvulnerabilityActive) return;
        if (health != null) health.EndInvulnerability();
        phaseInvulnerabilityActive = false;
    }

    // ---------------------------------------------------------------- 보조

    /// <summary>패턴 진행 상황을 콘솔에 남긴다. <see cref="logPatterns"/>가 켜져 있을 때만 동작한다.</summary>
    private void Trace(string message)
    {
        if (logPatterns) Debug.Log("[코뿌리] " + message, this);
    }

    /// <summary>공격 애니메이션이 없으므로 색으로 준비 상태를 알린다.</summary>
    private void SetWindupTint(bool on)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = on ? windupTint : Color.white;
    }

    /// <summary>멈춰 서 있는 동안에도 플레이어 쪽을 본다. 등을 돌린 채 시전하면 어색하다.</summary>
    private void FaceTowardPlayer()
    {
        if (enemyAnimator == null) return;
        enemyAnimator.SetActionState("Idle", DirectionTo(PlayerPosition));
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(direction.x * cos - direction.y * sin,
                           direction.x * sin + direction.y * cos);
    }
}
