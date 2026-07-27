using UnityEngine;

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
    [SerializeField, Min(0)] private int attackDamage = 1;
    [SerializeField, Min(0f)] private float attackCooldown = 1.0f;
    [SerializeField, Min(0)] private int goldReward = 2;
    [SerializeField, Min(0f)] private float knockbackStunDuration = 0.15f;
    [Tooltip("넉백 배율. 0이면 넉백 면역. 보스는 패턴 위치가 무너지지 않도록 0을 쓴다.")]
    [SerializeField, Min(0f)] private float knockbackMultiplier = 1f;
    [Tooltip("기본 추적 AI. 전용 보스 컨트롤러가 이동을 맡을 때만 끈다.")]
    [SerializeField] private bool basicAIEnabled = true;

    /// <summary>한 번 어그로가 끌리면 절대 풀리지 않는다.</summary>
    public bool IsAggro { get; private set; }

    /// <summary>
    /// 기본 추적 AI를 켜고 끈다. 전용 컨트롤러와 이 스크립트가 동시에 Rigidbody를 조작하면
    /// 서로 속도를 덮어써서 움직임이 망가지므로, 보스는 이걸 꺼 두고 직접 이동한다.
    /// </summary>
    public void SetBasicAIEnabled(bool enabled) => basicAIEnabled = enabled;

    private Rigidbody2D body;
    private Health health;
    private Collider2D ownCollider;
    private Transform player;
    private Health playerHealth;      // 매 FixedUpdate GetComponent 호출 방지용 캐시
    private Collider2D playerCollider;
    private float lastAttackTime = -999f;
    private float stunnedUntil = -999f;

    /// <summary>플레이어 공격 등으로 밀려나며 잠시 행동 불능이 된다.</summary>
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (health.IsDead) return;
        float scaled = force * knockbackMultiplier;
        // 넉백 면역이면 경직도 걸지 않는다. 경직만 남으면 보스 패턴이 끊긴다.
        if (scaled <= 0f) return;
        stunnedUntil = Time.time + knockbackStunDuration;
        body.linearVelocity = direction.normalized * scaled;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        ownCollider = GetComponent<Collider2D>();
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
        if (!basicAIEnabled) return;

        // 넉백 중에는 밀려나는 속도를 유지한다.
        if (Time.time < stunnedUntil) return;

        if (health.IsDead || player == null)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= detectRange) IsAggro = true;

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
            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            body.linearVelocity = direction * moveSpeed;
        }
        else
        {
            body.linearVelocity = Vector2.zero;
        }
    }

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

    private void HandleDeath()
    {
        body.linearVelocity = Vector2.zero;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        if (RunManager.Instance != null)
            RunManager.Instance.AddGold(goldReward);
        Destroy(gameObject, 0.4f);
    }
}
