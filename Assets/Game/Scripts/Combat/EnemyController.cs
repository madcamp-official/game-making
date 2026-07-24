using UnityEngine;

/// <summary>
/// 테스트용 적 AI. 플레이어가 감지 범위에 들어오면 추적하고,
/// 공격 범위에 들어오면 일정 주기로 피해를 준다. 죽으면 파괴된다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float detectRange = 6f;
    [SerializeField] private float attackRange = 1.0f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private int goldReward = 2;

    private Rigidbody2D body;
    private Health health;
    private Transform player;
    private float lastAttackTime = -999f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        health.OnDied += HandleDeath;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
    }

    private void FixedUpdate()
    {
        if (health.IsDead || player == null)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Health playerHealth = player.GetComponent<Health>();
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
                playerHealth?.TakeDamage(attackDamage);
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
