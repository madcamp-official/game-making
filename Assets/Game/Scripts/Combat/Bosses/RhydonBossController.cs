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
/// * 스톤샤워 — 방 전체에 돌을 떨어뜨린다. 안전 구역이 없어 그림자를 하나씩 읽고 비켜야 한다.
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
        [Tooltip("코뿌리가 발을 구르고 첫 돌이 떨어지기까지의 시간")]
        public float windup = 0.5f;
        [Tooltip("떨어뜨릴 돌 수. 방 전체에 흩어지므로 수가 곧 밀도다.")]
        public int stoneCount = 14;
        [Tooltip("돌을 하나씩 떨구는 간격")]
        public float spawnInterval = 0.17f;
        [Tooltip("그림자가 생기고 돌이 바닥에 닿기까지의 시간. 짧을수록 반응하기 어렵다.")]
        public float fallTime = 0.5f;
        [Tooltip("마지막 돌이 떨어진 뒤 후딜레이")]
        public float recovery = 0.5f;
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
    [Tooltip("걸어다닐 때의 속도. 플레이어는 5다. 코뿌리는 조금 느린 대신 돌진이 아주 빠르다.")]
    [SerializeField, Min(0f)] private float moveSpeed = 4.2f;
    [SerializeField, Min(0f)] private float introDelay = 0.15f;
    [Tooltip("전투 영역의 중심. 비워 두면 부모(방)의 위치를 쓴다.")]
    [SerializeField] private Transform arenaCenter;
    [Tooltip("전투 영역의 반너비·반높이. 돌과 돌진 목표를 이 안으로 제한한다. " +
             "벽 안쪽 면(RoomArena.HalfSize = ±7 · ±5)과 같아야 한다 — 좁게 잡으면 벽에 붙은 띠가 안전지대가 된다.")]
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(7f, 5f);
    [Tooltip("몸이 벽을 파고들지 않게 두는 여유. 이동과 돌진 도착점에만 쓴다. 돌 낙하는 벽까지 꽉 채운다.")]
    [SerializeField, Min(0f)] private float bodyMargin = RoomArena.BodyMargin;

    [Header("접근")]
    [Tooltip("뿔드릴을 쓰기 전에 이만큼까지 붙는다. 뿔 길이보다 짧아야 한다. " +
             "스톤샤워는 방 전체에 떨어지므로 미리 붙지 않는다.")]
    [SerializeField, Min(0f)] private float hornApproachDistance = 2.4f;
    [Tooltip("한 번의 접근에 쓸 수 있는 최대 시간. 벽에 막혀도 패턴이 무한히 밀리지 않게 한다.")]
    [SerializeField, Min(0f)] private float approachMaxDuration = 1.3f;

    [Header("패턴 사이 대기")]
    [SerializeField, Min(0f)] private float patternGapPhase1 = 0.45f;
    [SerializeField, Min(0f)] private float patternGapPhase2 = 0.32f;

    [Header("스톤샤워 — 1페이즈")]
    [SerializeField] private StoneSettings stonePhase1 = new StoneSettings
    {
        windup = 0.5f, stoneCount = 20,
        spawnInterval = 0.14f, fallTime = 0.75f, recovery = 0.5f,
    };

    // 2페이즈: 방 전체가 대상이라 "원을 넓히는" 강화는 뜻이 없어졌다. 대신 수를 크게 늘리고
    // 낙하 시간을 더 줄여, 같은 방에서 서 있을 자리가 계속 줄어들게 한다.
    [Header("스톤샤워 — 2페이즈")]
    [SerializeField] private StoneSettings stonePhase2 = new StoneSettings
    {
        windup = 0.42f, stoneCount = 30,
        spawnInterval = 0.11f, fallTime = 0.6f, recovery = 0.4f,
    };

    [Header("스톤샤워 — 공통")]
    [SerializeField, Min(0)] private int stoneDamage = 22;
    [Tooltip("돌 하나가 때리는 반지름")]
    [SerializeField, Min(0f)] private float stoneRadius = 0.74f;
    [Tooltip("착탄 판정이 남아 있는 시간. 짧게 두어 지나간 자리는 곧 안전해진다.")]
    [SerializeField, Min(0f)] private float stoneImpactDuration = 0.22f;
    [Tooltip("돌이 화면 위 어디에서부터 떨어지는지")]
    [SerializeField, Min(0f)] private float stoneDropHeight = 6f;
    [Tooltip("이 비율만큼은 플레이어가 지금 선 자리를 그대로 노린다.")]
    [SerializeField, Range(0f, 1f)] private float stoneAimAtPlayerRatio = 0.3f;
    [Tooltip("이 비율만큼은 플레이어 주변에 흩뿌린다. 나머지가 방 전체 무작위다. " +
             "제자리만 노리면 앞으로 걷는 것만으로 전부 피해지므로, 갈 곳까지 함께 덮는다.")]
    [SerializeField, Range(0f, 1f)] private float stoneNearPlayerRatio = 0.45f;
    [Tooltip("주변 낙하가 시작되는 거리. 이 안쪽은 제자리 조준이 맡는다.")]
    [SerializeField, Min(0f)] private float stoneScatterInner = 0.9f;
    [Tooltip("주변 낙하가 닿는 가장 먼 거리. 예고 시간 동안 걸어갈 수 있는 거리보다 넓어야 한다.")]
    [SerializeField, Min(0f)] private float stoneScatterOuter = 3.4f;

    [Header("뿔드릴 — 1페이즈")]
    [SerializeField] private HornSettings hornPhase1 = new HornSettings
    {
        chargeTime = 0.38f, length = 4.5f, baseWidth = 2.4f,
        thrustTime = 0.13f, holdTime = 0.1f, retractTime = 0.12f,
        stabs = 2, stabGap = 0.18f, lunge = 1.2f, recovery = 0.5f,
    };

    // 2페이즈: 뿔이 더 커지고 차지가 더 짧아지며, 한 번 더 찌른다.
    [Header("뿔드릴 — 2페이즈")]
    [SerializeField] private HornSettings hornPhase2 = new HornSettings
    {
        chargeTime = 0.32f, length = 5.4f, baseWidth = 3f,
        thrustTime = 0.12f, holdTime = 0.1f, retractTime = 0.1f,
        stabs = 3, stabGap = 0.16f, lunge = 1.5f, recovery = 0.4f,
    };

    [Header("뿔드릴 — 공통")]
    [SerializeField, Min(0)] private int hornDamage = 26;

    [Header("이판사판 — 1페이즈")]
    [SerializeField] private TakeDownSettings takeDownPhase1 = new TakeDownSettings
    {
        dashes = 3, aimTime = 0.3f, dashSpeed = 19.5f,
        dashMaxDuration = 1.2f, betweenDashes = 0.14f, recovery = 0.55f,
    };

    // 2페이즈: 한 번 더 돌진하고 더 빠르게 지나간다.
    [Header("이판사판 — 2페이즈")]
    [SerializeField] private TakeDownSettings takeDownPhase2 = new TakeDownSettings
    {
        dashes = 4, aimTime = 0.28f, dashSpeed = 24f,
        dashMaxDuration = 1.1f, betweenDashes = 0.12f, recovery = 0.45f,
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

    [Header("접촉 피해 — 꺼 둔다")]
    [Tooltip("몸이 닿기만 해도 자동으로 주는 피해. 0이면 접촉 피해가 없다. " +
             "잡몹과 같은 규칙으로 0에 둔다 — 피해는 예고가 보이는 기술에만 있어야 한다. " +
             "코앞을 때리는 몫은 뿔찌르기가 맡는다. 돌진 피해는 여기가 아니라 " +
             "TakeDownRoutine이 따로 낸다.")]
    [SerializeField, Min(0)] private int contactDamage;
    [SerializeField, Min(0f)] private float contactInterval = 1f;

    [Header("디버그")]
    [Tooltip("패턴 선택과 진행을 콘솔에 남긴다. 수치를 조정할 때만 켠다.")]
    [SerializeField] private bool logPatterns;

    [Header("연출 색상")]
    [Tooltip("페이즈 전환 충격파와 몸 색 변화에 쓰는 바위색")]
    [SerializeField] private Color rockColor = new Color(0.72f, 0.47f, 0.22f, 0.85f);
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
    /// <summary>지금 플레이어와의 충돌을 꺼 둔 상태인지 (돌진 중에만 참).</summary>
    private bool passingThroughPlayer;

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
    // 돌진 중 플레이어를 통과시키려면 이 둘의 충돌만 골라서 꺼야 한다. 벽과의 충돌은 남는다.
    private readonly List<Collider2D> ownColliders = new List<Collider2D>();
    private readonly List<Collider2D> playerColliders = new List<Collider2D>();
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

        CollectSolidColliders(GetComponentsInChildren<Collider2D>(true), ownColliders);

        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;
    }

    private void OnEnable()
    {
        // 기본 추적 AI와 이 컨트롤러가 동시에 Rigidbody를 만지면 안 된다.
        enemyController.SetBasicAIEnabled(false);
        // 돌진 도중에 꺼졌다면 통과 상태가 남아 있을 수 있다. 다시 켜질 때 초기값으로 되돌려
        // 다음 돌진이 끝날 때 반드시 원래대로 돌아오게 한다 (꺼진 콜라이더는 직접 만지지 않는다).
        passingThroughPlayer = false;
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
            CollectSolidColliders(pc.GetComponentsInChildren<Collider2D>(true), playerColliders);
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
        // 돌진 중에 죽으면 DashRoutine의 뒷정리가 돌지 않는다. 여기서 통과를 되돌린다.
        SetPassThroughPlayer(false);
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
                    // 돌은 방 전체에 떨어지므로 미리 붙을 이유가 없다.
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
    /// <remarks>
    /// 기준은 전투 영역이 아니라 몸이 놓일 수 있는 범위다. 벽 안쪽 면을 그대로 쓰면
    /// 몸이 벽에 막혀 그 선을 넘지 못하므로 이 밀어내기가 영영 걸리지 않는다.
    /// </remarks>
    private Vector2 InwardPush(Vector2 position)
    {
        Vector2 offset = position - ArenaCenter;
        Vector2 push = Vector2.zero;
        if (Mathf.Abs(offset.x) > arenaHalfSize.x - bodyMargin) push.x = -Mathf.Sign(offset.x);
        if (Mathf.Abs(offset.y) > arenaHalfSize.y - bodyMargin) push.y = -Mathf.Sign(offset.y);
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
    /// 코뿌리가 발을 구르면 <b>방 전체</b>에 돌이 떨어진다. 안전한 구역이 따로 없어서
    /// 도망칠 곳을 찾는 게 아니라 그림자를 하나씩 읽고 비켜 서야 한다.
    ///
    /// 예전에는 코뿌리 주변에만 원을 깔았는데, 그러면 "원 밖으로 나가면 끝"이라 답이 하나뿐이었다.
    /// 지금은 코뿌리 옆에 붙어서도 피할 수 있으니 계속 때릴 것인지 스스로 고르게 된다.
    /// </summary>
    private IEnumerator StoneShowerRoutine()
    {
        StoneSettings settings = inPhase2 ? stonePhase2 : stonePhase1;
        float fallTime = Mathf.Max(MinFallTime, settings.fallTime);

        holdPosition = true;
        body.linearVelocity = Vector2.zero;
        SetWindupTint(true);
        FaceTowardPlayer();

        int stones = Mathf.Max(1, settings.stoneCount);

        yield return new WaitForSeconds(settings.windup);
        SetWindupTint(false);
        // 시전 방향 고정을 풀지 않으면 패턴이 끝난 뒤에도 그 방향으로 굳은 채 걷는다.
        if (enemyAnimator != null) enemyAnimator.ClearActionState();
        if (health.IsDead) yield break;

        for (int i = 0; i < stones; i++)
        {
            if (health.IsDead) yield break;
            StartCoroutine(FallingStoneRoutine(NextStoneTarget(), fallTime, attackGeneration));
            if (i < stones - 1) yield return new WaitForSeconds(settings.spawnInterval);
        }

        Trace(string.Format("  스톤샤워: 방 전체 {0}개, 낙하 {1:0.00}초", stones, fallTime));

        // 마지막 돌이 떨어질 때까지 기다린 뒤 후딜레이. 안 그러면 후딜 중에 돌이 떨어진다.
        yield return new WaitForSeconds(fallTime);
        yield return new WaitForSeconds(settings.recovery);
    }

    /// <summary>
    /// 돌 하나가 떨어질 자리. 세 갈래로 나뉜다.
    /// * 플레이어가 지금 선 자리 — 제자리 버티기를 막는다.
    /// * 플레이어 주변 — 도망칠 방향까지 함께 덮는다. 이게 없으면 예고를 보고 앞으로
    ///   한 걸음 걷는 것만으로 전부 피해져, 패턴이 사실상 없는 것과 같아진다.
    /// * 방 전체 무작위 — 멀리 떨어져 있어도 안전지대가 생기지 않게 한다.
    /// </summary>
    private Vector2 NextStoneTarget()
    {
        if (player != null)
        {
            float roll = Random.value;
            if (roll < stoneAimAtPlayerRatio)
                return ClampToArena(PlayerPosition, stoneRadius);
            if (roll < stoneAimAtPlayerRatio + stoneNearPlayerRatio)
                return ClampToArena(PlayerPosition + RandomScatterOffset(), stoneRadius);
        }

        // 방 전체에 고르게 흩뿌린다.
        Vector2 center = ArenaCenter;
        Vector2 raw = new Vector2(
            center.x + Random.Range(-arenaHalfSize.x, arenaHalfSize.x),
            center.y + Random.Range(-arenaHalfSize.y, arenaHalfSize.y));
        return ClampToArena(raw, stoneRadius);
    }

    /// <summary>
    /// 플레이어를 둘러싼 고리 안의 한 점. 거리를 제곱근으로 펴야 넓은 바깥쪽이 성기지 않다 —
    /// 그냥 뽑으면 안쪽에만 몰려, 결국 제자리 조준과 같아진다.
    /// </summary>
    private Vector2 RandomScatterOffset()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float distance = Mathf.Lerp(stoneScatterInner, stoneScatterOuter, Mathf.Sqrt(Random.value));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
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
    ///
    /// 돌진 중에는 플레이어를 뚫고 지나간다 (<see cref="SetPassThroughPlayer"/>).
    /// 이때만이고, 걸어다닐 때는 평소처럼 몸이 부딪힌다.
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
        // 돌진할 때만 플레이어를 통과한다. 몸으로 막히면 방 반대편까지 밀고 나간다는
        // 패턴 자체가 성립하지 않고, 앞을 가로막고 서 있는 게 최선의 방어가 돼 버린다.
        SetPassThroughPlayer(true);
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

        SetPassThroughPlayer(false);
        dashing = false;
        holdPosition = true;
        body.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 플레이어와의 충돌만 껐다 켠다. 벽·다른 적과의 충돌은 그대로 남으므로
    /// 돌진이 방 밖으로 새지 않는다.
    /// </summary>
    private void SetPassThroughPlayer(bool ignore)
    {
        if (passingThroughPlayer == ignore) return;
        passingThroughPlayer = ignore;

        for (int i = 0; i < ownColliders.Count; i++)
        {
            Collider2D mine = ownColliders[i];
            if (mine == null) continue;
            for (int j = 0; j < playerColliders.Count; j++)
            {
                Collider2D theirs = playerColliders[j];
                if (theirs == null) continue;
                Physics2D.IgnoreCollision(mine, theirs, ignore);
            }
        }
    }

    /// <summary>몸을 막는 콜라이더만 모은다. 트리거는 상호작용용이라 건드리면 안 된다.</summary>
    private static void CollectSolidColliders(Collider2D[] source, List<Collider2D> into)
    {
        into.Clear();
        foreach (Collider2D collider in source)
            if (collider != null && !collider.isTrigger) into.Add(collider);
    }

    /// <summary>돌진 중 플레이어를 들이받았는지. 무적 시간이 연타를 막아 준다.</summary>
    private void TryDashHit()
    {
        if (player == null || playerHealth == null) return;
        if (playerHealth.IsDead || playerHealth.IsInvincible) return;
        if (Vector2.Distance(player.position, transform.position) > dashHitRadius) return;

        playerHealth.TakeDamage(dashDamage);
    }

    /// <summary>
    /// 주어진 방향으로 전투 영역 경계까지 갔을 때의 지점. 돌진의 도착점이다.
    ///
    /// 몸이 실제로 설 수 있는 자리까지만 잡는다 (<see cref="bodyMargin"/>). 벽 안쪽 면을
    /// 그대로 목표로 삼으면 몸이 벽에 걸려 영영 도착하지 못하고, 돌진이 매번
    /// <c>dashMaxDuration</c>을 다 쓰고서야 끝난다.
    /// </summary>
    private Vector2 ArenaEdgePoint(Vector2 origin, Vector2 direction)
    {
        Vector2 center = ArenaCenter;
        Vector2 offset = origin - center;
        Vector2 reach = new Vector2(Mathf.Max(0f, arenaHalfSize.x - bodyMargin),
                                    Mathf.Max(0f, arenaHalfSize.y - bodyMargin));
        // 경계에 닿기까지 갈 수 있는 거리를 축별로 구해 더 짧은 쪽을 쓴다.
        float distance = Mathf.Max(reach.x, reach.y) * 2f;
        if (Mathf.Abs(direction.x) > 0.0001f)
        {
            float limit = (Mathf.Sign(direction.x) * reach.x - offset.x) / direction.x;
            if (limit > 0f) distance = Mathf.Min(distance, limit);
        }
        if (Mathf.Abs(direction.y) > 0.0001f)
        {
            float limit = (Mathf.Sign(direction.y) * reach.y - offset.y) / direction.y;
            if (limit > 0f) distance = Mathf.Min(distance, limit);
        }
        return origin + direction * Mathf.Max(0.5f, distance);
    }

    // ---------------------------------------------------------------- 페이즈 전환

    private IEnumerator PhaseTransitionRoutine()
    {
        holdPosition = true;
        dashing = false;
        SetPassThroughPlayer(false);
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
                spriteRenderer.color = Color.Lerp(Color.white, rockColor, t);
            yield return null;
        }
        transform.localScale = baseScale;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (health.IsDead) yield break;

        // 피해 없는 충격파
        AttackTelegraph wave = AttackTelegraph.CreateRing(
            attackRoot, transform.position, 0.6f, rockColor);
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
}
