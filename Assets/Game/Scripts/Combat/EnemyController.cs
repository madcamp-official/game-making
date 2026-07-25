using UnityEngine;

/// <summary>
/// 테스트용 적 AI. 플레이어가 감지 범위에 들어오면 추적하고,
/// 공격 범위에 들어오면 일정 주기로 피해를 준다. 죽으면 파괴된다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float detectRange = 6f;
    [SerializeField, Min(0f)] private float attackRange = 1.0f;
    [SerializeField, Min(0)] private int attackDamage = 1;
    [SerializeField, Min(0f)] private float attackCooldown = 1.0f;
    [SerializeField, Min(0)] private int goldReward = 2;
    [SerializeField, Min(0f)] private float knockbackStunDuration = 0.15f;

    private Rigidbody2D body;
    private Health health;
    private Transform player;
    private Health playerHealth; // 매 FixedUpdate GetComponent 호출 방지용 캐시
    private float lastAttackTime = -999f;
    private float stunnedUntil = -999f;

    /// <summary>플레이어 공격 등으로 밀려나며 잠시 행동 불능이 된다.</summary>
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (health.IsDead) return;
        stunnedUntil = Time.time + knockbackStunDuration;
        body.linearVelocity = direction.normalized * force;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        health.OnDied += HandleDeath;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
        }
    }

    private void FixedUpdate()
    {
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

        if (distance <= attackRange)
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;
                if (playerHealth != null) playerHealth.TakeDamage(attackDamage);
            }
        }
        else if (distance <= detectRange)
        {
            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            body.linearVelocity = direction * moveSpeed;
        }
        else
        {
            body.linearVelocity = Vector2.zero;
        }
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
