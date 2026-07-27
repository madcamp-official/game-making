using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1층 보스 버터플의 전투 로직. 명세는 `docs/boss-butterfree-spec.md` (2차 개편안).
///
/// 패턴은 두 페이즈 모두 바람탄·독가루·은빛바람 세 종류뿐이고, 2페이즈는 새 패턴이 아니라
/// 같은 세 패턴의 강화형만 쓴다. 난도는 패턴을 섞어서가 아니라 한 패턴 안의 연속 동작으로 만든다.
/// 서로 다른 패턴은 절대 겹치지 않는다.
///
/// <see cref="EnemyController"/>의 기본 추적 AI와 Rigidbody를 동시에 조작하면 안 되므로,
/// 이 컴포넌트가 켜질 때 기본 AI를 끈다. 일반 적은 그대로 기본 AI로 움직인다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody2D))]
public class ButterfreeBossController : MonoBehaviour
{
    private enum BossState
    {
        Intro, Reposition, PatternCooldown, Windup, Executing, Recovery, PhaseTransition, Dead
    }

    private enum Pattern { WindBullet, PoisonCloud, SilverWind }

    /// <summary>바람탄 한 번의 발사가 어디를 노리는지.</summary>
    private enum WindAim
    {
        /// <summary>예고 시작 시점의 플레이어 위치.</summary>
        Current,
        /// <summary>플레이어의 현재 속도로 조금 앞을 예측.</summary>
        Predict,
        /// <summary>후보 방향 중 인접 둘을 비워 통과할 문을 만든다.</summary>
        Gate,
    }

    // ---------------------------------------------------------------- 패턴 설정

    [System.Serializable]
    private class WindVolley
    {
        [Tooltip("이 발사의 예고 시간")]
        public float windup = 0.5f;
        [Tooltip("발사할 탄 수. Gate에서는 후보 방향 수다.")]
        public int count = 3;
        [Tooltip("탄 사이 각도")]
        public float stepAngle = 16f;
        [Tooltip("이 발사 뒤 다음 발사의 예고를 시작하기까지의 간격")]
        public float gapAfter = 0.35f;
        public WindAim aim = WindAim.Current;
    }

    [System.Serializable]
    private class WindSettings
    {
        public WindVolley[] volleys;
        public float speed = 6f;
        [Tooltip("마지막 발사 뒤 후딜레이")]
        public float recovery = 1f;
        [Tooltip("Predict 조준이 내다보는 시간")]
        public float predictLeadTime = 0.3f;
        [Tooltip("Predict 조준의 최대 보정 거리")]
        public float predictMaxDistance = 1.5f;
    }

    [System.Serializable]
    private class PoisonSettings
    {
        [Tooltip("기록할 플레이어 위치 수")]
        public int count = 3;
        [Tooltip("위치를 기록하는 간격")]
        public float recordInterval = 0.4f;
        [Tooltip("예고 원이 뜬 뒤 장판으로 바뀌기까지의 시간")]
        public float activationDelay = 0.75f;
        public float radius = 0.9f;
        [Tooltip("장판 유지 시간")]
        public float duration = 2.7f;
        [Tooltip("마지막 장판이 사라진 뒤 후딜레이")]
        public float recovery = 1f;
        [Tooltip("이 번째 장판만 이동 방향 앞을 노린다. 음수면 사용하지 않는다.")]
        public int predictIndex = -1;
        [Tooltip("앞을 노릴 때의 거리")]
        public float predictLead = 1.2f;
    }

    [System.Serializable]
    private class SilverSettings
    {
        [Tooltip("파동 수")]
        public int waves = 2;
        [Tooltip("비워 둘 인접 슬롯 수. 12슬롯 기준 4면 안전 구역이 120도다.")]
        public int safeSlots = 4;
        public float firstWindup = 0.8f;
        [Tooltip("두 번째 이후 파동의 예고 시간")]
        public float laterWindup = 0.65f;
        [Tooltip("파동이 나간 뒤 다음 예고를 시작하기까지의 간격")]
        public float waveGap = 0.7f;
        [Tooltip("파동마다 안전 구역이 도는 각도. 한 패턴 안에서는 방향이 바뀌지 않는다.")]
        public float rotationStep = 60f;
        public float speed = 4.8f;
        [Tooltip("마지막 파동 뒤 후딜레이")]
        public float recovery = 1f;
    }

    // 명세 5절의 최소 반응 시간. Inspector에서 아무리 줄여도 이 아래로는 내려가지 않는다.
    private const float MinAimWindup = 0.40f;
    private const float MinZoneWindup = 0.55f;
    private const float MinSilverFirstWindup = 0.65f;
    private const float MinSilverLaterWindup = 0.50f;
    private const float MinPhase2Recovery = 0.70f;

    // 최대 체력은 Health 컴포넌트의 값을 그대로 쓴다 (프리팹에서 240).
    [Header("기본")]
    [Tooltip("이동 속도. 플레이어는 5다.")]
    [SerializeField, Min(0f)] private float moveSpeed = 4.2f;
    [SerializeField, Min(0f)] private float introDelay = 1f;
    [Tooltip("전투 영역의 중심. 비워 두면 부모(방)의 위치를 쓴다.")]
    [SerializeField] private Transform arenaCenter;
    [Tooltip("전투 영역의 반너비·반높이. 장판과 이동 목표를 이 안으로 제한한다.")]
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(8.2f, 5.2f);

    [Header("거리 유지")]
    [SerializeField, Min(0f)] private float preferredMinDistance = 3.5f;
    [SerializeField, Min(0f)] private float preferredMaxDistance = 5f;
    [Tooltip("한 번의 위치 조정에 쓸 수 있는 최대 시간. 벽에 막혀도 패턴이 무한히 밀리지 않게 한다.")]
    [SerializeField, Min(0f)] private float repositionMaxDuration = 1.5f;
    [SerializeField, Min(0f)] private float orbitDuration = 0.45f;

    [Header("패턴 사이 대기")]
    [SerializeField, Min(0f)] private float patternGapPhase1 = 1f;
    [SerializeField, Min(0f)] private float patternGapPhase2 = 0.8f;

    [Header("바람탄 — 1페이즈")]
    [SerializeField] private WindSettings windPhase1 = new WindSettings
    {
        volleys = new[]
        {
            new WindVolley { windup = 0.55f, count = 3, stepAngle = 16f, gapAfter = 0.35f, aim = WindAim.Current },
            new WindVolley { windup = 0.50f, count = 5, stepAngle = 12f, gapAfter = 0f,    aim = WindAim.Current },
        },
        speed = 6f,
        recovery = 1f,
    };

    [Header("바람탄 — 2페이즈")]
    [SerializeField] private WindSettings windPhase2 = new WindSettings
    {
        volleys = new[]
        {
            new WindVolley { windup = 0.45f, count = 5, stepAngle = 9f,  gapAfter = 0.25f, aim = WindAim.Current },
            new WindVolley { windup = 0.40f, count = 5, stepAngle = 7f,  gapAfter = 0.25f, aim = WindAim.Predict },
            new WindVolley { windup = 0.45f, count = 6, stepAngle = 12f, gapAfter = 0f,    aim = WindAim.Gate },
        },
        speed = 6.5f,
        recovery = 0.8f,
    };

    [Header("바람탄 — 공통")]
    [SerializeField, Min(0)] private int windDamage = 10;
    [SerializeField, Min(0f)] private float windLifetime = 3f;
    [SerializeField, Min(0f)] private float windRadius = 0.18f;

    [Header("독가루 — 1페이즈")]
    [SerializeField] private PoisonSettings poisonPhase1 = new PoisonSettings
    {
        count = 3, recordInterval = 0.4f, activationDelay = 0.75f,
        radius = 0.9f, duration = 2.7f, recovery = 1f, predictIndex = -1,
    };

    [Header("독가루 — 2페이즈")]
    [SerializeField] private PoisonSettings poisonPhase2 = new PoisonSettings
    {
        count = 5, recordInterval = 0.28f, activationDelay = 0.6f,
        radius = 0.95f, duration = 3f, recovery = 0.8f, predictIndex = 3, predictLead = 1.2f,
    };

    [Header("독가루 — 공통")]
    [SerializeField, Min(0)] private int poisonDamage = 8;
    [SerializeField, Min(0f)] private float poisonTickInterval = 1f;
    [Tooltip("직전 장판 중심과 최소한 이만큼 떨어뜨린다. 더 가까우면 이동 방향으로 밀거나 생략한다.")]
    [SerializeField, Min(0f)] private float poisonMinSeparation = 0.85f;
    [Tooltip("장판 중심을 방 경계에서 이만큼 안쪽으로 유지한다.")]
    [SerializeField, Min(0f)] private float poisonArenaMargin = 0.55f;
    [Tooltip("예고 중인 것까지 포함한 장판 수 상한.")]
    [SerializeField, Min(1)] private int poisonMaxZones = 6;
    [Tooltip("장판이 전투 영역에서 차지할 수 있는 최대 면적 비율.")]
    [SerializeField, Range(0.1f, 1f)] private float poisonMaxAreaRatio = 0.55f;

    [Header("은빛바람 — 1페이즈")]
    [SerializeField] private SilverSettings silverPhase1 = new SilverSettings
    {
        waves = 2, safeSlots = 4, firstWindup = 0.8f, laterWindup = 0.65f,
        waveGap = 0.7f, rotationStep = 60f, speed = 4.8f, recovery = 1f,
    };

    [Header("은빛바람 — 2페이즈")]
    [SerializeField] private SilverSettings silverPhase2 = new SilverSettings
    {
        waves = 3, safeSlots = 3, firstWindup = 0.65f, laterWindup = 0.5f,
        waveGap = 0.55f, rotationStep = 60f, speed = 5.3f, recovery = 0.9f,
    };

    [Header("은빛바람 — 공통")]
    [Tooltip("원을 나누는 슬롯 수. 12면 슬롯 간격이 30도다.")]
    [SerializeField, Min(4)] private int silverSlotCount = 12;
    [SerializeField, Min(0)] private int silverDamage = 12;
    [SerializeField, Min(0f)] private float silverLifetime = 4f;
    [SerializeField, Min(0f)] private float silverRadius = 0.18f;
    [Tooltip("안전 부채꼴 표시의 반지름.")]
    [SerializeField, Min(0f)] private float silverTelegraphRadius = 3.2f;

    [Header("투사체 풀")]
    [Tooltip("미리 만들어 둘 적 투사체 수. 동시에 이 수를 넘겨 발사하지 않는다.")]
    [SerializeField, Min(1)] private int projectilePoolSize = 48;

    [Header("페이즈 전환")]
    [Tooltip("2페이즈에 들어간 뒤 공격하지 않고 두는 시간. 전환을 인지할 여유를 준다.")]
    [SerializeField, Min(0f)] private float phase2GraceTime = 0.8f;

    [Header("접촉 피해")]
    [SerializeField, Min(0)] private int contactDamage = 10;
    [SerializeField, Min(0f)] private float contactInterval = 1f;

    [Header("디버그")]
    [Tooltip("패턴 선택과 발사 내역을 콘솔에 남긴다. 수치를 조정할 때만 켠다.")]
    [SerializeField] private bool logPatterns;

    [Header("연출 색상")]
    [SerializeField] private Color warningColor = new Color(1f, 0.9f, 0.25f, 0.45f);
    [SerializeField] private Color windColor = new Color(0.75f, 0.95f, 1f, 1f);
    [SerializeField] private Color poisonWarningColor = new Color(0.75f, 0.35f, 0.85f, 0.35f);
    [SerializeField] private Color poisonZoneColor = new Color(0.45f, 0.12f, 0.6f, 0.65f);
    [SerializeField] private Color silverColor = new Color(0.6f, 1f, 0.95f, 1f);
    // 숲 바닥이 초록이라 초록 계열 안전 표시는 거의 보이지 않는다. 파랑 계열로 대비를 준다.
    [SerializeField] private Color safeZoneColor = new Color(0.3f, 0.7f, 1f, 0.4f);
    [SerializeField] private Color windupTint = new Color(1f, 0.7f, 0.7f, 1f);

    private const float TelegraphLineLength = 5f;
    private const float ProjectileSpawnOffset = 0.6f;

    private BossState state = BossState.Intro;
    private bool inPhase2;
    private bool phaseTransitionPending;
    private bool phaseTransitionDone;

    private EnemyController enemyController;
    private Health health;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Health playerHealth;
    private Rigidbody2D playerBody;
    private Vector2 fallbackArenaCenter;
    private float nextContactDamageTime;

    /// <summary>보스가 만든 모든 오브젝트의 부모. 사망·방 이동 시 통째로 지운다.</summary>
    private Transform attackRoot;
    private EnemyProjectilePool pool;
    private readonly List<DamageZone> activeZones = new List<DamageZone>();
    /// <summary>예고 원만 떠 있고 아직 장판이 되지 않은 수. 상한 계산에 함께 센다.</summary>
    private int pendingZones;

    // 셔플 백. 세 패턴을 한 번씩 다 쓰기 전에는 같은 패턴이 다시 나오지 않는다.
    private readonly List<Pattern> bag = new List<Pattern>(3);
    private bool hasLastPattern;
    private Pattern lastPattern;

    private Vector2 ArenaCenter => arenaCenter != null ? (Vector2)arenaCenter.position : fallbackArenaCenter;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        fallbackArenaCenter = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;

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
        // 파괴 중이면 EnemyController가 이미 사라졌을 수 있다.
        if (enemyController != null) enemyController.SetBasicAIEnabled(true);
    }

    /// <summary>공격 중에는 절대 움직이지 않는다. 플레이어와 부딪혀 밀리는 것도 막는다.</summary>
    private void Update()
    {
        if (state == BossState.Windup || state == BossState.Executing ||
            state == BossState.Recovery || state == BossState.Dead)
            body.linearVelocity = Vector2.zero;
    }

    private void Start()
    {
        // 플레이어 참조는 여기서 한 번만 얻어 전투 내내 재사용한다.
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
            playerBody = pc.GetComponent<Rigidbody2D>();
        }

        // 배율 1인 씬 루트에 둔다. 보스(1.3배) 아래에 두면 탄과 장판까지 커진다.
        attackRoot = new GameObject("Butterfree_Attacks").transform;
        pool = EnemyProjectilePool.Create(attackRoot, projectilePoolSize);
        pool.SetArena(ArenaCenter, arenaHalfSize);

        StartCoroutine(Battle());
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
        // 방을 넘어갈 때 공격 오브젝트가 씬에 남으면 안 된다.
        // 씬 자체가 내려가는 중이면 정리할 필요가 없다.
        if (attackRoot != null && gameObject.scene.isLoaded) Destroy(attackRoot.gameObject);
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
        state = BossState.Dead;
        StopAllCoroutines();
        body.linearVelocity = Vector2.zero;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        // 예고, 남은 탄, 장판을 전부 지워 사망 후 추가 피해가 없게 한다.
        ClearAttackObjects();
        if (attackRoot != null) Destroy(attackRoot.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (state == BossState.Dead || contactDamage <= 0) return;
        if (Time.time < nextContactDamageTime) return;
        if (collision.collider.GetComponentInParent<PlayerController>() == null) return;
        if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return;

        playerHealth.TakeDamage(contactDamage);
        nextContactDamageTime = Time.time + contactInterval;
        // 접촉 피해는 패턴 진행에 관여하지 않는다.
    }

    // ---------------------------------------------------------------- 전투 흐름

    private IEnumerator Battle()
    {
        state = BossState.Intro;
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(introDelay);

        while (!health.IsDead)
        {
            if (phaseTransitionPending && !phaseTransitionDone)
            {
                phaseTransitionDone = true;
                yield return PhaseTransitionRoutine();
                continue;
            }

            state = BossState.PatternCooldown;
            body.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(inPhase2 ? patternGapPhase2 : patternGapPhase1);
            if (health.IsDead) yield break;

            Pattern next = DrawPattern();

            // 은빛바람은 사방으로 퍼지므로 방 중앙에서 쏜다.
            if (next == Pattern.SilverWind) yield return MoveTo(ArenaCenter, repositionMaxDuration, 0.5f);
            else yield return RepositionRoutine();
            if (health.IsDead) yield break;

            lastPattern = next;
            hasLastPattern = true;
            Trace((inPhase2 ? "P2 " : "P1 ") + next + " 시작 (남은 백 " + bag.Count + ")");
            switch (next)
            {
                case Pattern.WindBullet: yield return WindBulletRoutine(); break;
                case Pattern.PoisonCloud: yield return PoisonCloudRoutine(); break;
                case Pattern.SilverWind: yield return SilverWindRoutine(); break;
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
        bag.Add(Pattern.WindBullet);
        bag.Add(Pattern.PoisonCloud);
        bag.Add(Pattern.SilverWind);

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

    /// <summary>페이즈 전환·사망에서 남은 탄과 장판, 예고를 한 번에 없앤다.</summary>
    private void ClearAttackObjects()
    {
        if (pool != null) pool.ReturnAll();

        for (int i = activeZones.Count - 1; i >= 0; i--)
            if (activeZones[i] != null) Destroy(activeZones[i].gameObject);
        activeZones.Clear();
        pendingZones = 0;

        if (attackRoot == null) return;
        foreach (AttackTelegraph telegraph in attackRoot.GetComponentsInChildren<AttackTelegraph>(true))
            Destroy(telegraph.gameObject);
    }

    // ---------------------------------------------------------------- 이동

    /// <summary>선호 거리대로 들어간 뒤 잠깐 선회한다. 최대 시간을 넘기면 그대로 끝낸다.</summary>
    private IEnumerator RepositionRoutine()
    {
        state = BossState.Reposition;
        float deadline = Time.time + repositionMaxDuration;
        float orbitSign = Random.value < 0.5f ? 1f : -1f;

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
            float distance = toPlayer.magnitude;
            if (distance >= preferredMinDistance && distance <= preferredMaxDistance) break;

            float sign = distance > preferredMaxDistance ? 1f : -1f;
            body.linearVelocity = toPlayer.normalized * sign * moveSpeed;
            yield return null;
        }

        // 남은 시간 안에서 짧게만 선회한다. 매 패턴 전에 1.5초를 다 쓰면 전투가 늘어진다.
        float orbitEnd = Mathf.Min(deadline, Time.time + orbitDuration);
        while (Time.time < orbitEnd && !health.IsDead && player != null)
        {
            Vector2 self = transform.position;
            Vector2 inward = InwardPush(self);
            Vector2 toPlayer = (Vector2)player.position - self;
            Vector2 direction = inward != Vector2.zero
                ? inward
                : new Vector2(-toPlayer.y, toPlayer.x).normalized * orbitSign;
            body.linearVelocity = direction * moveSpeed;
            yield return null;
        }

        body.linearVelocity = Vector2.zero;
    }

    private IEnumerator MoveTo(Vector2 target, float maxDuration, float stopDistance)
    {
        state = BossState.Reposition;
        float deadline = Time.time + maxDuration;
        while (Time.time < deadline && !health.IsDead)
        {
            Vector2 toTarget = target - (Vector2)transform.position;
            if (toTarget.magnitude <= stopDistance) break;
            body.linearVelocity = toTarget.normalized * moveSpeed;
            yield return null;
        }
        body.linearVelocity = Vector2.zero;
    }

    /// <summary>벽에 몰렸으면 전투 영역 안쪽으로 향하는 방향, 아니면 0.</summary>
    private Vector2 InwardPush(Vector2 position)
    {
        Vector2 center = ArenaCenter;
        Vector2 offset = position - center;
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

    private Vector2 PlayerPosition => player != null ? (Vector2)player.position : ArenaCenter;

    /// <summary>플레이어의 현재 이동 방향. 멈춰 있으면 0.</summary>
    private Vector2 PlayerMoveDirection
    {
        get
        {
            if (playerBody == null) return Vector2.zero;
            Vector2 velocity = playerBody.linearVelocity;
            return velocity.sqrMagnitude > 0.04f ? velocity.normalized : Vector2.zero;
        }
    }

    private Vector2 DirectionTo(Vector2 target)
    {
        Vector2 delta = target - (Vector2)transform.position;
        return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
    }

    // ---------------------------------------------------------------- 패턴 1 · 바람탄

    /// <summary>
    /// 여러 번의 부채꼴을 연속으로 쏜다. 발사마다 예고 시작 시점에 새로 조준하므로
    /// 첫 회피 방향을 그대로 유지하면 다음 발사에 맞는다.
    /// </summary>
    private IEnumerator WindBulletRoutine()
    {
        WindSettings settings = inPhase2 ? windPhase2 : windPhase1;
        if (settings.volleys == null || settings.volleys.Length == 0) yield break;

        for (int v = 0; v < settings.volleys.Length; v++)
        {
            WindVolley volley = settings.volleys[v];

            // 조준은 이 발사의 예고가 시작될 때 확정하고, 예고 중에는 갱신하지 않는다.
            Vector2 target = AimTarget(volley.aim, settings);
            Vector2 origin = transform.position;
            Vector2 aim = DirectionTo(target);

            // Gate는 후보 중 인접한 둘을 비운다. 비운 자리가 플레이어가 지날 문이다.
            bool hasGate = volley.aim == WindAim.Gate && volley.count >= 3;
            int gateStart = hasGate ? Random.Range(0, volley.count - 1) : 0;

            state = BossState.Windup;
            body.linearVelocity = Vector2.zero;
            SetWindupTint(true);

            float windup = Mathf.Max(MinAimWindup, volley.windup);
            for (int i = 0; i < volley.count; i++)
            {
                if (hasGate && (i == gateStart || i == gateStart + 1)) continue;   // 문은 예고선도 그리지 않는다
                Vector2 direction = Rotate(aim, AngleAt(i, volley.count, volley.stepAngle));
                // 예고선 두께는 실제 탄 지름과 같게 둔다. 경고가 위험 범위보다 얇으면 안 된다.
                AttackTelegraph line = AttackTelegraph.CreateLine(
                    attackRoot, origin, direction, TelegraphLineLength, windRadius * 2f, warningColor);
                line.Pulse(windup);
            }
            yield return new WaitForSeconds(windup);
            SetWindupTint(false);
            if (health.IsDead) yield break;

            state = BossState.Executing;
            int fired = 0;
            for (int i = 0; i < volley.count; i++)
            {
                if (hasGate && (i == gateStart || i == gateStart + 1)) continue;
                Vector2 direction = Rotate(aim, AngleAt(i, volley.count, volley.stepAngle));
                FireProjectile(origin + direction * ProjectileSpawnOffset, direction,
                    settings.speed, windDamage, windLifetime, windRadius, windColor);
                fired++;
            }
            Trace(string.Format("  바람탄 {0}번째: {1} 조준, 예고 {2:0.00}초, {3}발",
                v + 1, volley.aim, windup, fired));

            // 마지막 발사가 아니면 다음 예고까지 잠깐 쉰다.
            if (v < settings.volleys.Length - 1 && volley.gapAfter > 0f)
            {
                state = BossState.Recovery;
                yield return new WaitForSeconds(volley.gapAfter);
                if (health.IsDead) yield break;
            }
        }

        state = BossState.Recovery;
        yield return new WaitForSeconds(PatternRecovery(settings.recovery));
    }

    /// <summary>조준 방식에 따른 목표 지점.</summary>
    private Vector2 AimTarget(WindAim aim, WindSettings settings)
    {
        Vector2 current = PlayerPosition;
        if (aim != WindAim.Predict) return current;

        // 정지 중이면 예측할 게 없으므로 현재 위치를 그대로 쓴다.
        if (playerBody == null) return current;
        Vector2 velocity = playerBody.linearVelocity;
        if (velocity.sqrMagnitude <= 0.04f) return current;

        Vector2 lead = velocity * settings.predictLeadTime;
        if (lead.magnitude > settings.predictMaxDistance)
            lead = lead.normalized * settings.predictMaxDistance;
        return current + lead;
    }

    /// <summary>부채꼴에서 i번째 탄의 중심 대비 각도. 3발·16도면 -16, 0, +16이 된다.</summary>
    private static float AngleAt(int index, int count, float step)
    {
        return (index - (count - 1) * 0.5f) * step;
    }

    // ---------------------------------------------------------------- 패턴 2 · 독가루

    /// <summary>
    /// 플레이어가 지나간 자리를 순서대로 막는다. 한꺼번에 뿌리지 않고 일정 간격으로 기록해,
    /// 계속 같은 방향으로 달리면 스스로 길을 막게 만든다.
    /// </summary>
    private IEnumerator PoisonCloudRoutine()
    {
        PoisonSettings settings = inPhase2 ? poisonPhase2 : poisonPhase1;
        float windup = Mathf.Max(MinZoneWindup, settings.activationDelay);

        state = BossState.Windup;
        body.linearVelocity = Vector2.zero;
        SetWindupTint(true);

        PruneZones();
        bool hasPrevious = false;
        Vector2 previous = Vector2.zero;
        float lastZoneEnd = Time.time;
        int placedCount = 0;

        for (int i = 0; i < settings.count; i++)
        {
            if (health.IsDead) yield break;

            // 4번째만 이동 방향 앞을 노려 진행 방향을 한 번 끊는다.
            Vector2 raw = PlayerPosition;
            if (i == settings.predictIndex)
            {
                Vector2 moveDir = PlayerMoveDirection;
                if (moveDir != Vector2.zero) raw += moveDir * settings.predictLead;
            }

            if (TryPlaceZone(raw, hasPrevious, previous, settings, out Vector2 placed))
            {
                previous = placed;
                hasPrevious = true;

                AttackTelegraph warning = AttackTelegraph.CreateCircle(
                    attackRoot, placed, settings.radius, poisonWarningColor);
                warning.Pulse(windup);
                pendingZones++;
                StartCoroutine(ActivateZoneAfter(placed, windup, settings));
                lastZoneEnd = Mathf.Max(lastZoneEnd, Time.time + windup + settings.duration);
                placedCount++;
            }

            if (i < settings.count - 1) yield return new WaitForSeconds(settings.recordInterval);
        }

        SetWindupTint(false);
        Trace(string.Format("  독가루: {0}회 시도 중 {1}개 배치, 예고 {2:0.00}초",
            settings.count, placedCount, windup));

        // 마지막 장판이 사라질 때까지 다음 패턴을 시작하지 않는다 (패턴 중첩 금지).
        // 이 시간이 플레이어가 보스를 때릴 수 있는 구간이기도 하다.
        state = BossState.Recovery;
        float wait = lastZoneEnd - Time.time;
        if (wait > 0f) yield return new WaitForSeconds(wait);
        if (health.IsDead) yield break;

        yield return new WaitForSeconds(PatternRecovery(settings.recovery));
    }

    /// <summary>예고 원이 다 깜빡인 뒤 실제 장판으로 바꾼다.</summary>
    private IEnumerator ActivateZoneAfter(Vector2 center, float delay, PoisonSettings settings)
    {
        yield return new WaitForSeconds(delay);
        pendingZones = Mathf.Max(0, pendingZones - 1);
        if (health.IsDead) yield break;

        DamageZone zone = DamageZone.Spawn(attackRoot, center, settings.radius, settings.duration,
            poisonDamage, poisonTickInterval, poisonZoneColor);
        activeZones.Add(zone);
    }

    /// <summary>
    /// 명세 7.3의 배치 규칙을 적용한다. 둘 수 없으면 그 장판을 생략한다.
    /// 장판은 기본적으로 플레이어가 이미 지나온 자리라서, 규칙만 지키면 가둘 일이 없다.
    /// </summary>
    private bool TryPlaceZone(Vector2 raw, bool hasPrevious, Vector2 previous,
                              PoisonSettings settings, out Vector2 placed)
    {
        placed = ClampToArena(raw, settings.radius + poisonArenaMargin);

        PruneZones();
        // 명세는 "활성화했거나 예고 중인" 장판을 함께 세라고 한다.
        int occupied = activeZones.Count + pendingZones;
        if (occupied >= poisonMaxZones) return false;

        // 장판이 방을 너무 많이 덮으면 더 놓지 않는다.
        float arenaArea = arenaHalfSize.x * 2f * arenaHalfSize.y * 2f;
        float zoneArea = Mathf.PI * settings.radius * settings.radius * (occupied + 1);
        if (arenaArea > 0f && zoneArea / arenaArea > poisonMaxAreaRatio) return false;

        if (!hasPrevious) return true;

        Vector2 delta = placed - previous;
        if (delta.magnitude >= poisonMinSeparation) return true;

        // 너무 가까우면 플레이어 이동 방향으로 민다. 밀 방향이 없으면 생략한다.
        Vector2 pushDirection = PlayerMoveDirection;
        if (pushDirection == Vector2.zero)
            pushDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.zero;
        if (pushDirection == Vector2.zero) return false;

        Vector2 pushed = ClampToArena(previous + pushDirection * poisonMinSeparation,
                                      settings.radius + poisonArenaMargin);
        // 벽에 막혀 밀어도 여전히 겹치면 그냥 생략한다.
        if ((pushed - previous).magnitude < poisonMinSeparation * 0.9f) return false;

        placed = pushed;
        return true;
    }

    private void PruneZones()
    {
        for (int i = activeZones.Count - 1; i >= 0; i--)
            if (activeZones[i] == null) activeZones.RemoveAt(i);
    }

    // ---------------------------------------------------------------- 패턴 3 · 은빛바람

    /// <summary>
    /// 보스 주변에서 원형으로 퍼지는 탄막. 인접한 몇 개 슬롯을 비워 안전 부채꼴을 만들고,
    /// 파동마다 같은 방향으로 회전시켜 그 구역을 따라 이동하게 한다.
    /// </summary>
    private IEnumerator SilverWindRoutine()
    {
        SilverSettings settings = inPhase2 ? silverPhase2 : silverPhase1;
        Vector2 origin = transform.position;

        int slots = Mathf.Max(4, silverSlotCount);
        int safeSlots = Mathf.Clamp(settings.safeSlots, 1, slots - 1);
        float slotStep = 360f / slots;
        float safeSweep = safeSlots * slotStep;

        // 회전 방향은 패턴 시작 시 한 번 정하고 끝까지 바꾸지 않는다.
        float rotationSign = Random.value < 0.5f ? 1f : -1f;
        float rotation = settings.rotationStep * rotationSign;
        // 안전 구역의 첫 위치는 플레이어 쪽으로 잡아, 처음부터 갇힌 채 시작하지 않게 한다.
        int safeStart = Mathf.RoundToInt(Vector2.SignedAngle(Vector2.right, DirectionTo(PlayerPosition)) / slotStep);

        state = BossState.Windup;
        body.linearVelocity = Vector2.zero;

        for (int wave = 0; wave < settings.waves; wave++)
        {
            if (health.IsDead) yield break;

            float windup = wave == 0
                ? Mathf.Max(MinSilverFirstWindup, settings.firstWindup)
                : Mathf.Max(MinSilverLaterWindup, settings.laterWindup);

            // 안전 구역의 중심 각도. 비우는 슬롯이 짝수면 슬롯 경계가 중심이 된다.
            float safeCenter = (safeStart + (safeSlots - 1) * 0.5f) * slotStep;

            state = BossState.Windup;
            SetWindupTint(true);

            // 파동당 원형 예고 1개 + 안전 부채꼴 1개만 쓴다 (탄마다 예고선을 만들지 않는다).
            AttackTelegraph ring = AttackTelegraph.CreateRing(
                attackRoot, origin, silverTelegraphRadius, silverColor * 0.8f);
            ring.Pulse(windup);
            // 안전 표시는 깜빡이지 않는다. 어두워지는 순간에 안 보이면 안전 구역을 읽을 수 없다.
            AttackTelegraph safe = AttackTelegraph.CreateSector(
                attackRoot, origin, silverTelegraphRadius, safeCenter, safeSweep, safeZoneColor);
            safe.Hold(windup);

            // 첫 예고에서만 안전 구역이 어느 쪽으로 돌지 흐릿한 부채꼴로 보여 준다.
            if (wave == 0 && settings.waves > 1)
            {
                AttackTelegraph hint = AttackTelegraph.CreateSector(
                    attackRoot, origin, silverTelegraphRadius * 0.75f, safeCenter, safeSweep,
                    safeZoneColor * 0.6f);
                hint.SweepTo(safeCenter, safeCenter + rotation, windup);
            }

            yield return new WaitForSeconds(windup);
            SetWindupTint(false);
            if (health.IsDead) yield break;

            state = BossState.Executing;
            for (int i = 0; i < slots; i++)
            {
                // 안전 슬롯은 비운다. 예고한 부채꼴과 정확히 같은 범위여야 한다.
                int relative = ((i - safeStart) % slots + slots) % slots;
                if (relative < safeSlots) continue;

                Vector2 direction = Rotate(Vector2.right, i * slotStep);
                FireProjectile(origin + direction * ProjectileSpawnOffset, direction,
                    settings.speed, silverDamage, silverLifetime, silverRadius, silverColor);
            }

            Trace(string.Format("  은빛바람 {0}파동: 예고 {1:0.00}초, {2}발, 안전 중심 {3:0}도 / 폭 {4:0}도",
                wave + 1, windup, slots - safeSlots, safeCenter, safeSweep));
            safeStart += Mathf.RoundToInt(rotation / slotStep);

            if (wave < settings.waves - 1)
            {
                state = BossState.Recovery;
                yield return new WaitForSeconds(settings.waveGap);
            }
        }

        state = BossState.Recovery;
        yield return new WaitForSeconds(PatternRecovery(settings.recovery));
    }

    // ---------------------------------------------------------------- 페이즈 전환

    private IEnumerator PhaseTransitionRoutine()
    {
        state = BossState.PhaseTransition;
        // 전환 연출 중에는 새 공격 판정이 남아 있으면 안 된다.
        ClearAttackObjects();

        yield return MoveTo(ArenaCenter, repositionMaxDuration, 0.5f);
        if (health.IsDead) yield break;

        body.linearVelocity = Vector2.zero;
        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < 1f && !health.IsDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed);
            transform.localScale = baseScale * Mathf.Lerp(1f, 1.18f, t);
            if (spriteRenderer != null)
                spriteRenderer.color = Color.Lerp(Color.white, silverColor, t);
            yield return null;
        }
        transform.localScale = baseScale;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (health.IsDead) yield break;

        // 피해 없는 바람 파동
        AttackTelegraph wave = AttackTelegraph.CreateRing(attackRoot, transform.position, 0.6f, silverColor);
        wave.Expand(0.6f, 6f, 0.6f);
        yield return new WaitForSeconds(0.6f);
        if (health.IsDead) yield break;

        inPhase2 = true;
        Trace("2페이즈 전환 — 남은 공격 오브젝트 정리, 셔플 백 초기화");
        // 강화 패턴을 새 셔플 백으로 다시 뽑는다. 첫 패턴을 특정 패턴으로 고정하지 않는다.
        bag.Clear();
        hasLastPattern = false;

        // 전환을 인지할 시간을 준다.
        yield return new WaitForSeconds(phase2GraceTime);
    }

    // ---------------------------------------------------------------- 보조

    /// <summary>2페이즈 후딜레이는 아무리 줄여도 하한 아래로 내려가지 않는다.</summary>
    private float PatternRecovery(float value)
    {
        return inPhase2 ? Mathf.Max(MinPhase2Recovery, value) : value;
    }

    /// <summary>풀에서 투사체를 빌려 발사한다. 남은 게 없으면 그 발사는 건너뛴다.</summary>
    private void FireProjectile(Vector2 position, Vector2 direction, float speed, int damage,
                                float lifetime, float radius, Color color)
    {
        if (pool == null) return;
        EnemyProjectile projectile = pool.Borrow();
        if (projectile == null) return;
        projectile.Launch(position, direction, speed, damage, lifetime, radius, color);
    }

    /// <summary>패턴 진행 상황을 콘솔에 남긴다. <see cref="logPatterns"/>가 켜져 있을 때만 동작한다.</summary>
    private void Trace(string message)
    {
        if (logPatterns) Debug.Log("[버터플] " + message, this);
    }

    /// <summary>공격 애니메이션이 없으므로 색으로 준비 상태를 알린다.</summary>
    private void SetWindupTint(bool on)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = on ? windupTint : Color.white;
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
