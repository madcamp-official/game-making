using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 적 AI. 플레이어가 감지 범위에 들어오거나 한 번이라도 피해를 입으면 추적을 시작하고,
/// 이후에는 거리와 관계없이 추적을 멈추지 않는다.
/// 공격 판정은 중심 거리뿐 아니라 콜라이더 표면 사이 거리로도 확인해서,
/// 덩치 큰 적에게 대각선으로 바짝 붙어도 공격이 들어간다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float detectRange = 6f;
    [SerializeField, Min(0f)] private float attackRange = 1.0f;
    [Tooltip("콜라이더 표면 사이 거리 기준 공격 범위. 중심 거리가 멀어도 몸이 이만큼 붙으면 공격한다.")]
    [SerializeField, Min(0f)] private float attackContactReach = 0.25f;
    [Tooltip("몸이 닿는 거리에서 자동으로 주는 피해. 0이면 접촉 피해가 없다 — 1·3층 잡몹은 " +
             "피할 수 있는 전용 근접기로만 때리므로 일부러 0으로 둔다 (MeleeAttackSetup).")]
    [SerializeField, Min(0)] private int attackDamage = 1;
    [SerializeField, Min(0f)] private float attackCooldown = 1.0f;
    [Tooltip("처치 보상 골드의 하한. 최댓값과 같게 두면 고정 보상이 된다 (보스가 그렇다).")]
    [FormerlySerializedAs("goldReward")]
    [SerializeField, Min(0)] private int goldRewardMin = 2;
    [Tooltip("처치 보상 골드의 상한. 하한보다 작으면 하한만 쓴다.")]
    [SerializeField, Min(0)] private int goldRewardMax = 3;
    [Tooltip("이 거리를 유지하려 한다. 0이면 그냥 플레이어에게 붙는다. " +
             "원거리 적이 근접전에 말려들지 않게 하는 값이다.")]
    [SerializeField, Min(0f)] private float keepDistance;
    [SerializeField, Min(0f)] private float knockbackStunDuration = 0.15f;
    [Tooltip("넉백 배율. 0이면 넉백 면역. 보스는 패턴 위치가 무너지지 않도록 0을 쓴다.")]
    [SerializeField, Min(0f)] private float knockbackMultiplier = 1f;
    [Tooltip("몸으로 밀 수 없게 한다. 보스만 켠다 — 잡몹은 밀어내며 헤집는 맛이 있어야 한다. " +
             "넉백 면역과는 다른 이야기다. 넉백은 공격이 넣는 속도라 knockbackMultiplier가 막지만, " +
             "이건 두 몸이 겹쳤을 때 물리 엔진이 밀어내는 것이라 따로 막아야 한다.")]
    [SerializeField] private bool immovable;
    [Tooltip("기본 추적 AI. 전용 보스 컨트롤러가 이동을 맡을 때만 끈다.")]
    [SerializeField] private bool basicAIEnabled = true;
    [Tooltip("방에 들어온 순간부터 추적을 시작한다. 끄면 감지 범위에 들어와야 움직인다.")]
    [SerializeField] private bool aggroOnSpawn = true;

    /// <summary>한 번 어그로가 끌리면 절대 풀리지 않는다.</summary>
    public bool IsAggro { get; private set; }

    /// <summary>
    /// 추적을 시작했고 아직 싸울 수 있는 상태. 벽이나 플레이어에 막혀 제자리여도 참이다.
    /// 애니메이터가 이걸 보고 걷기를 유지한다 — 실제 속도로만 판단하면 몸이 닿는 순간
    /// 속도가 0이 되면서 멈춰 선 그림으로 바뀐다.
    /// </summary>
    public bool IsEngaged { get; private set; }

    /// <summary>지금 향하고 있는 방향. 막혀서 속도가 0이어도 마지막 방향을 유지한다.</summary>
    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    /// <summary>아직 넉백으로 밀려나는 중인지. 이 동안에는 능력을 시전하지 않는다.</summary>
    public bool IsKnockedBack => knockbackActive && Time.time < stunnedUntil;

    /// <summary>지금 발이 묶여 있는지. 그림에 찬 기운을 입힐지 정하는 데 쓴다.</summary>
    public bool IsSlowed => Time.time < slowUntil;

    /// <summary>
    /// 발을 묶는다. 걷는 속도에만 걸린다 — 이미 시작한 돌진이나 잠수처럼 능력이 직접
    /// 속도를 넣는 동작은 그대로 간다. <b>내지른 것은 끝까지 간다</b>가 맞고, 그래야
    /// 예고를 보고 자리를 잡는 판단이 감속 여부로 뒤집히지 않는다.
    ///
    /// 보스에게는 저절로 닿지 않는다. 보스는 <see cref="basicAIEnabled"/>가 꺼져 있어
    /// 전용 컨트롤러가 직접 움직이므로, 걷는 속도를 깎아도 쓰이는 곳이 없다.
    /// 보스 이동까지 늦추면 패턴 사이 거리 계산이 통째로 무너진다.
    /// </summary>
    /// <param name="multiplier">남는 속도의 비율. 0.55면 45% 느려진다.</param>
    public void ApplySlow(float multiplier, float duration)
    {
        if (health.IsDead || duration <= 0f) return;

        float clamped = Mathf.Clamp01(multiplier);
        // 겹쳐 걸리면 더 센 쪽(더 작은 배율)을 남기고, 시간은 늘 늘어난다.
        // 약한 감속을 나중에 걸었다고 강한 감속이 풀리면 순서에 따라 결과가 달라진다.
        if (!IsSlowed || clamped < slowMultiplier) slowMultiplier = clamped;
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
        // 다시 칠하게 한다. 돌진처럼 몸빛을 흰색으로 되돌리는 능력이 사이에 끼면
        // 걸려 있는데도 색이 지워진 채로 남는다.
        tintedSlow = false;
    }

    /// <summary>
    /// 기본 추적 AI를 켜고 끈다. 전용 컨트롤러와 이 스크립트가 동시에 Rigidbody를 조작하면
    /// 서로 속도를 덮어써서 움직임이 망가지므로, 보스는 이걸 꺼 두고 직접 이동한다.
    /// </summary>
    public void SetBasicAIEnabled(bool enabled) => basicAIEnabled = enabled;

    /// <summary>
    /// 지금 기본 추적 AI가 켜져 있는지. 시전이 끝난 능력이 "켜짐"으로 복원해 버리면
    /// 원래부터 추적하지 않는 적(닥트리오)까지 걸어다니게 되므로, 이전 값을 읽어 되돌린다.
    /// </summary>
    public bool BasicAIEnabled => basicAIEnabled;

    private Rigidbody2D body;
    private Health health;
    private Collider2D ownCollider;
    private Transform player;
    private Health playerHealth;      // 매 FixedUpdate GetComponent 호출 방지용 캐시
    private Collider2D playerCollider;
    private float lastAttackTime = -999f;
    private float stunnedUntil = -999f;
    /// <summary>넉백으로 넣은 속도가 아직 남아 있는지.</summary>
    private bool knockbackActive;
    private float slowUntil = -999f;
    private float slowMultiplier = 1f;
    private SpriteRenderer spriteRenderer;
    private bool tintedSlow;

    /// <summary>발이 묶인 동안 입히는 찬 기운. 눈에 보이지 않으면 걸렸는지 알 수 없다.</summary>
    private static readonly Color SlowTint = new Color(0.62f, 0.78f, 1f);

    /// <summary>플레이어 공격 등으로 밀려나며 잠시 행동 불능이 된다.</summary>
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (health.IsDead) return;
        float scaled = force * knockbackMultiplier;
        // 넉백 면역이면 경직도 걸지 않는다. 경직만 남으면 보스 패턴이 끊긴다.
        if (scaled <= 0f) return;
        stunnedUntil = Time.time + knockbackStunDuration;
        knockbackActive = true;
        body.linearVelocity = direction.normalized * scaled;
    }

    /// <summary>밀려나던 속도를 거둔다. 시전 중인 능력이 있으면 다음 프레임에 제 속도를 다시 넣는다.</summary>
    private void EndKnockback()
    {
        knockbackActive = false;
        body.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// <see cref="immovable"/>일 때 쓰는 질량. 물리 엔진은 겹친 두 몸을 밀어낼 때 각자의
    /// <b>질량에 반비례해서</b> 나눠 옮기므로, 플레이어(질량 1)의 1000배면 밀리는 양이
    /// 0.1%가 되어 사실상 붙박이다.
    ///
    /// Kinematic으로 바꾸지 않는 이유: Kinematic은 벽(Static)과도 충돌하지 않아 전투장을
    /// 그대로 빠져나간다. 버터플의 <c>InwardPush</c>는 "벽에 막혀 더 못 나간다"를 전제로
    /// 짜여 있다. 질량만 올리면 벽에는 그대로 막히면서 플레이어에게만 안 밀린다.
    /// </summary>
    private const float ImmovableMass = 1000f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        ownCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        // 프리팹 값에 기대지 않고 여기서 못박는다. 눈에 띄지 않는 수치라 한 번 어긋나면
        // "이 보스만 밀린다"가 되고, 원인을 프리팹에서 찾기 어렵다.
        if (immovable) body.mass = ImmovableMass;
        health.OnDied += HandleDeath;
        health.OnDamaged += HandleDamaged;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
            playerCollider = pc.GetComponent<Collider2D>();
        }
        // 방에 들어서는 순간 전부 달려든다. 감지 범위를 기다리면 방 반대편의 적은
        // 플레이어가 걸어올 때까지 가만히 서 있어서 한 마리씩 상대하게 된다.
        if (aggroOnSpawn) IsAggro = true;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDeath;
            health.OnDamaged -= HandleDamaged;
        }
    }

    // 감지 범위 밖에서 원거리 공격을 맞아도 즉시 어그로가 끌린다.
    private void HandleDamaged() => IsAggro = true;

    private void FixedUpdate()
    {
        // 전용 컨트롤러가 이동을 맡는 동안에는 Rigidbody를 건드리지 않는다.
        // 단 넉백만은 여기서 끝낸다 — 안 그러면 되돌릴 주체가 없어 맞은 적이 방 끝까지 미끄러진다.
        // (닥트리오처럼 기본 AI를 끄고 능력으로만 움직이는 적에서 실제로 드러난다.)
        if (!basicAIEnabled)
        {
            if (knockbackActive && Time.time >= stunnedUntil) EndKnockback();
            return;
        }

        // 넉백 중에는 밀려나는 속도를 유지한다.
        if (Time.time < stunnedUntil) return;
        knockbackActive = false;

        // 플레이어가 조작할 수 없는 동안에는 멈춰 선다 (방에 걸어 들어오는 연출).
        // 넉백보다 뒤에 두는 것이 중요하다 — 밀려나던 것까지 여기서 멈추면 안 된다.
        if (CombatFreeze.Active)
        {
            IsEngaged = false;
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (health.IsDead || player == null)
        {
            IsEngaged = false;
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            IsEngaged = false;
            body.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= detectRange) IsAggro = true;

        IsEngaged = IsAggro;
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        if (toPlayer.sqrMagnitude > 0.0001f) FacingDirection = toPlayer.normalized;

        if (IsInAttackRange(distance))
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;
                if (playerHealth != null) playerHealth.TakeDamage(attackDamage);
            }
        }
        else if (IsAggro)
        {
            body.linearVelocity = DesiredVelocity(distance);
        }
        else
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 추적 속도. <see cref="keepDistance"/>가 0이면 곧장 다가가고, 값이 있으면
    /// 그 거리를 사이에 두고 너무 가까우면 물러난다. 딱 그 거리 부근(±<see cref="DistanceDeadZone"/>)에서는
    /// 멈춘다 — 안 그러면 경계선에서 앞뒤로 떨리기만 한다.
    /// </summary>
    private Vector2 DesiredVelocity(float distance)
    {
        float speed = moveSpeed * (IsSlowed ? slowMultiplier : 1f);
        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        if (keepDistance <= 0f) return toPlayer * speed;

        float gap = distance - keepDistance;
        if (Mathf.Abs(gap) <= DistanceDeadZone) return Vector2.zero;
        return toPlayer * (gap > 0f ? speed : -speed);
    }

    /// <summary>
    /// 발이 묶인 동안 몸에 찬 기운을 입힌다. 풀리면 한 번만 되돌린다.
    ///
    /// 매 프레임 덮어쓰는 것이 아니라 <b>상태가 바뀔 때만</b> 손대는 것이 중요하다.
    /// 돌진처럼 잠시 몸빛을 바꾸는 능력이 있어서(<c>EnemyDashAbility</c>), 계속 덮어쓰면
    /// 그쪽 연출이 아예 보이지 않는다. 감속이 걸리고 풀리는 순간에만 칠한다.
    /// </summary>
    private void Update()
    {
        if (spriteRenderer == null || health.IsDead) return;

        bool slowed = IsSlowed;
        if (slowed == tintedSlow) return;
        tintedSlow = slowed;
        spriteRenderer.color = slowed ? SlowTint : Color.white;
    }

    private const float DistanceDeadZone = 0.35f;

    /// <summary>
    /// 중심 거리 또는 콜라이더 표면 거리 중 하나라도 만족하면 공격 범위 안이다.
    /// 중심 거리만 쓰면 덩치 큰 적의 대각선 모서리에 붙었을 때 몸이 닿아 있는데도
    /// 중심 사이가 멀어서 공격이 나가지 않았다.
    /// </summary>
    private bool IsInAttackRange(float centerDistance)
    {
        if (centerDistance <= attackRange) return true;
        if (ownCollider == null || playerCollider == null) return false;
        if (!ownCollider.enabled || !playerCollider.enabled) return false;

        ColliderDistance2D gap = ownCollider.Distance(playerCollider);
        return gap.isValid && gap.distance <= attackContactReach; // 겹쳐 있으면 음수
    }

    /// <summary>
    /// 처치할 때마다 새로 뽑는 보상 골드. 하한~상한 균등이라 <b>기댓값은 정확히 한가운데</b>다 —
    /// 층별 예산은 이 한가운데 값을 마릿수만큼 더해 맞춘다.
    ///
    /// 마리마다 흔들려도 한 층에 스무 마리쯤 잡으므로 총합의 흔들림은 √20만큼 작아진다.
    /// 한 마리가 ±50%로 흔들려도 층 수입은 대략 ±7%(1σ)에 머문다.
    /// 보스는 한 번뿐이라 그 완충이 없으므로 상·하한을 같게 두어 고정한다.
    /// </summary>
    private int RollGold()
    {
        int high = Mathf.Max(goldRewardMin, goldRewardMax);
        return high > goldRewardMin ? Random.Range(goldRewardMin, high + 1) : goldRewardMin;
    }

    private void HandleDeath()
    {
        // 찬 기운을 입은 채로 쓰러지면 사라지는 0.4초 동안 파랗게 남는다.
        if (spriteRenderer != null && tintedSlow) spriteRenderer.color = Color.white;
        body.linearVelocity = Vector2.zero;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        RunStats.CountKill();
        if (RunManager.Instance != null)
            RunManager.Instance.AddGold(RollGold());
        Destroy(gameObject, 0.4f);
    }
}
