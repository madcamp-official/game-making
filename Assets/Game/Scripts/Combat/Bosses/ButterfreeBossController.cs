using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1층 보스 버터플의 전투 로직. 명세는 `docs/boss-butterfree-spec.md`.
///
/// 플레이어를 직접 쫓지 않고 일정 거리를 유지하다가, 예고 → 발사 → 빈틈 순서로 패턴을 쓴다.
/// 모든 공격에는 반드시 예고가 먼저 보이고, 예고 방향과 실제 발사 방향은 같다.
/// 체력 절반에서 한 번 2페이즈로 전환하며 은빛바람이 해금된다.
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

    private enum Pattern { None, WindBullet, PoisonCloud, SilverWind }

    // 최대 체력은 Health 컴포넌트의 값을 그대로 쓴다 (프리팹에서 240).
    [Header("기본")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.8f;
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

    [Header("바람탄")]
    [SerializeField, Min(0f)] private float windWindup = 0.7f;
    [SerializeField, Min(0f)] private float windRecovery = 0.8f;
    [SerializeField, Min(0f)] private float windSpread = 20f;
    [SerializeField, Min(0f)] private float windSpreadPhase2 = 16f;
    [SerializeField, Min(1)] private int windCount = 3;
    [SerializeField, Min(1)] private int windCountPhase2 = 5;
    [SerializeField, Min(0f)] private float windSpeed = 6f;
    [SerializeField, Min(0)] private int windDamage = 10;
    [SerializeField, Min(0f)] private float windLifetime = 3f;
    [SerializeField, Min(0f)] private float windRadius = 0.18f;

    [Header("독가루 장판")]
    [SerializeField, Min(0f)] private float poisonWindup = 0.9f;
    [SerializeField, Min(0f)] private float poisonRecovery = 1f;
    [SerializeField, Min(1)] private int poisonCount = 3;
    [SerializeField, Min(1)] private int poisonCountPhase2 = 4;
    [SerializeField, Min(0f)] private float poisonRadius = 0.9f;
    [SerializeField, Min(0f)] private float poisonDuration = 3f;
    [SerializeField, Min(0)] private int poisonDamage = 8;
    [SerializeField, Min(0f)] private float poisonTickInterval = 1f;
    [Tooltip("첫 장판(플레이어 위치)에서 나머지 장판까지의 거리.")]
    [SerializeField, Min(0f)] private float poisonSpreadDistance = 1.7f;

    [Header("은빛바람 (2페이즈 전용)")]
    [SerializeField, Min(0f)] private float silverWindup = 1f;
    [SerializeField, Min(0f)] private float silverRecovery = 1.2f;
    [SerializeField, Min(1)] private int silverCountPerVolley = 8;
    [SerializeField, Min(0f)] private float silverVolleyGap = 0.45f;
    [SerializeField, Min(0f)] private float silverSpeed = 5f;
    [SerializeField, Min(0)] private int silverDamage = 12;
    [SerializeField, Min(0f)] private float silverLifetime = 4f;
    [SerializeField, Min(0f)] private float silverRadius = 0.18f;

    [Header("2페이즈 패턴 가중치")]
    [SerializeField, Min(0f)] private float windWeight = 40f;
    [SerializeField, Min(0f)] private float poisonWeight = 35f;
    [SerializeField, Min(0f)] private float silverWeight = 25f;
    [Tooltip("남아 있는 장판이 이 수 이상이면 독가루를 후보에서 뺀다.")]
    [SerializeField, Min(1)] private int maxActiveZones = 2;

    [Header("접촉 피해")]
    [SerializeField, Min(0)] private int contactDamage = 10;
    [SerializeField, Min(0f)] private float contactInterval = 1f;

    [Header("연출 색상")]
    [SerializeField] private Color warningColor = new Color(1f, 0.9f, 0.25f, 0.45f);
    [SerializeField] private Color windColor = new Color(0.75f, 0.95f, 1f, 1f);
    [SerializeField] private Color poisonWarningColor = new Color(0.75f, 0.35f, 0.85f, 0.35f);
    [SerializeField] private Color poisonZoneColor = new Color(0.45f, 0.12f, 0.6f, 0.65f);
    [SerializeField] private Color silverColor = new Color(0.6f, 1f, 0.95f, 1f);
    [SerializeField] private Color windupTint = new Color(1f, 0.7f, 0.7f, 1f);

    private const float TelegraphLineLength = 5f;
    private const float ProjectileSpawnOffset = 0.6f;

    private BossState state = BossState.Intro;
    private Pattern lastPattern = Pattern.None;
    private bool inPhase2;
    private bool phaseTransitionPending;
    private bool phaseTransitionDone;

    private EnemyController enemyController;
    private Health health;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Health playerHealth;
    private Vector2 fallbackArenaCenter;
    private float nextContactDamageTime;

    /// <summary>보스가 만든 모든 오브젝트의 부모. 사망·방 이동 시 통째로 지운다.</summary>
    private Transform attackRoot;
    private readonly List<DamageZone> activeZones = new List<DamageZone>();
    private readonly List<Pattern> candidates = new List<Pattern>(3);
    private readonly List<float> candidateWeights = new List<float>(3);

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
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
        }

        // 배율 1인 씬 루트에 둔다. 보스(1.3배) 아래에 두면 탄과 장판까지 커진다.
        attackRoot = new GameObject("Butterfree_Attacks").transform;

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
        if (attackRoot != null) Destroy(attackRoot.gameObject);
        activeZones.Clear();
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

            Pattern next = ChoosePattern();

            // 은빛바람은 사방으로 퍼지므로 방 중앙에서 쏜다.
            if (next == Pattern.SilverWind) yield return MoveTo(ArenaCenter, repositionMaxDuration, 0.5f);
            else yield return RepositionRoutine();
            if (health.IsDead) yield break;

            lastPattern = next;
            switch (next)
            {
                case Pattern.WindBullet: yield return WindBulletRoutine(); break;
                case Pattern.PoisonCloud: yield return PoisonCloudRoutine(); break;
                case Pattern.SilverWind: yield return SilverWindRoutine(); break;
            }
        }
    }

    /// <summary>직전 패턴과 현재 페이즈를 반영해 다음 패턴을 고른다.</summary>
    private Pattern ChoosePattern()
    {
        if (!inPhase2)
            return lastPattern == Pattern.WindBullet ? Pattern.PoisonCloud : Pattern.WindBullet;

        PruneZones();
        candidates.Clear();
        candidateWeights.Clear();

        AddCandidate(Pattern.WindBullet, windWeight);
        // 장판이 이미 많으면 방을 막지 않도록 후보에서 뺀다.
        if (activeZones.Count < maxActiveZones) AddCandidate(Pattern.PoisonCloud, poisonWeight);
        AddCandidate(Pattern.SilverWind, silverWeight);

        if (candidates.Count == 0) return Pattern.WindBullet;

        float total = 0f;
        for (int i = 0; i < candidateWeights.Count; i++) total += candidateWeights[i];
        if (total <= 0f) return candidates[0];

        float roll = Random.Range(0f, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidateWeights[i];
            if (roll <= 0f) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    /// <summary>직전에 쓴 패턴은 후보에 넣지 않는다 (같은 패턴 연속 사용 금지).</summary>
    private void AddCandidate(Pattern pattern, float weight)
    {
        if (pattern == lastPattern || weight <= 0f) return;
        candidates.Add(pattern);
        candidateWeights.Add(weight);
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

    private Vector2 AimDirection()
    {
        if (player == null) return Vector2.right;
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        return toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
    }

    // ---------------------------------------------------------------- 패턴 1 · 바람탄

    private IEnumerator WindBulletRoutine()
    {
        int count = inPhase2 ? windCountPhase2 : windCount;
        float step = inPhase2 ? windSpreadPhase2 : windSpread;

        // 예고 시작 시점의 방향을 저장하고, 발사까지 바꾸지 않는다.
        Vector2 aim = AimDirection();
        Vector2 origin = transform.position;

        state = BossState.Windup;
        body.linearVelocity = Vector2.zero;
        SetWindupTint(true);

        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Rotate(aim, AngleAt(i, count, step));
            // 예고선 두께는 실제 탄 지름과 같게 둔다. 경고가 위험 범위보다 얇으면 안 된다.
            AttackTelegraph line = AttackTelegraph.CreateLine(
                attackRoot, origin, direction, TelegraphLineLength, windRadius * 2f, warningColor);
            line.Pulse(windWindup);
        }
        yield return new WaitForSeconds(windWindup);
        SetWindupTint(false);
        if (health.IsDead) yield break;

        state = BossState.Executing;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Rotate(aim, AngleAt(i, count, step));
            EnemyProjectile.Spawn(attackRoot, origin + direction * ProjectileSpawnOffset, direction,
                windSpeed, windDamage, windLifetime, windRadius, windColor);
        }

        state = BossState.Recovery;
        yield return new WaitForSeconds(windRecovery);
    }

    /// <summary>부채꼴에서 i번째 탄의 중심 대비 각도. 3발·20도면 -20, 0, +20이 된다.</summary>
    private static float AngleAt(int index, int count, float step)
    {
        return (index - (count - 1) * 0.5f) * step;
    }

    // ---------------------------------------------------------------- 패턴 2 · 독가루

    private IEnumerator PoisonCloudRoutine()
    {
        int count = inPhase2 ? poisonCountPhase2 : poisonCount;

        // 첫 장판은 예고 시작 시점의 플레이어 위치. 이후 플레이어를 따라가지 않는다.
        Vector2 anchor = player != null ? (Vector2)player.position : ArenaCenter;
        Vector2[] positions = new Vector2[count];
        positions[0] = ClampToArena(anchor, poisonRadius);

        float baseAngle = Random.Range(0f, 360f);
        for (int i = 1; i < count; i++)
        {
            // 나머지는 첫 장판 주위에 고르게 둔다. 사이에 빠져나갈 틈이 남는다.
            float angle = baseAngle + (i - 1) * (360f / Mathf.Max(1, count - 1));
            Vector2 offset = Rotate(Vector2.right, angle) * poisonSpreadDistance;
            positions[i] = ClampToArena(anchor + offset, poisonRadius);
        }

        state = BossState.Windup;
        body.linearVelocity = Vector2.zero;
        SetWindupTint(true);

        for (int i = 0; i < count; i++)
        {
            AttackTelegraph warning = AttackTelegraph.CreateCircle(
                attackRoot, positions[i], poisonRadius, poisonWarningColor);
            warning.Pulse(poisonWindup);
        }
        yield return new WaitForSeconds(poisonWindup);
        SetWindupTint(false);
        if (health.IsDead) yield break;

        state = BossState.Executing;
        PruneZones();
        for (int i = 0; i < count; i++)
        {
            DamageZone zone = DamageZone.Spawn(attackRoot, positions[i], poisonRadius, poisonDuration,
                poisonDamage, poisonTickInterval, poisonZoneColor);
            activeZones.Add(zone);
        }

        state = BossState.Recovery;
        yield return new WaitForSeconds(poisonRecovery);
    }

    private void PruneZones()
    {
        for (int i = activeZones.Count - 1; i >= 0; i--)
            if (activeZones[i] == null) activeZones.RemoveAt(i);
    }

    // ---------------------------------------------------------------- 패턴 3 · 은빛바람

    private IEnumerator SilverWindRoutine()
    {
        Vector2 origin = transform.position;
        float step = 360f / silverCountPerVolley;

        state = BossState.Windup;
        body.linearVelocity = Vector2.zero;
        SetWindupTint(true);

        AttackTelegraph gather = AttackTelegraph.CreateRing(attackRoot, origin, 1.6f, silverColor * 0.8f);
        gather.Pulse(silverWindup);
        for (int i = 0; i < silverCountPerVolley; i++)
        {
            Vector2 direction = Rotate(Vector2.right, i * step);
            AttackTelegraph line = AttackTelegraph.CreateLine(
                attackRoot, origin, direction, TelegraphLineLength, silverRadius * 2f, warningColor);
            line.Pulse(silverWindup);
        }
        yield return new WaitForSeconds(silverWindup);
        SetWindupTint(false);
        if (health.IsDead) yield break;

        state = BossState.Executing;
        FireRadialVolley(origin, step, 0f);
        yield return new WaitForSeconds(silverVolleyGap);
        if (health.IsDead) yield break;
        // 2차는 1차 사이의 빈틈을 향한다. 추가 조준은 하지 않는다.
        FireRadialVolley(origin, step, step * 0.5f);

        state = BossState.Recovery;
        yield return new WaitForSeconds(silverRecovery);
    }

    private void FireRadialVolley(Vector2 origin, float step, float angleOffset)
    {
        for (int i = 0; i < silverCountPerVolley; i++)
        {
            Vector2 direction = Rotate(Vector2.right, angleOffset + i * step);
            EnemyProjectile.Spawn(attackRoot, origin + direction * ProjectileSpawnOffset, direction,
                silverSpeed, silverDamage, silverLifetime, silverRadius, silverColor);
        }
    }

    // ---------------------------------------------------------------- 페이즈 전환

    private IEnumerator PhaseTransitionRoutine()
    {
        state = BossState.PhaseTransition;
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
        lastPattern = Pattern.None;
        yield return SilverWindRoutine();
        lastPattern = Pattern.SilverWind;
    }

    // ---------------------------------------------------------------- 보조

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
