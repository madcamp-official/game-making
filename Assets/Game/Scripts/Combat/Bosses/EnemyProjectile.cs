using UnityEngine;

/// <summary>
/// 적이 쏘는 직선 투사체. 플레이어에게만 피해를 준다.
///
/// 플레이어의 <see cref="Projectile"/>과 대칭되는 반대편 구현이지만, 이쪽은 한 패턴에
/// 수십 발이 나가므로 <see cref="EnemyProjectilePool"/>에서 빌려 쓴다. 오브젝트와 컴포넌트는
/// 준비 단계에서 한 번만 만들고, 발사할 때는 <see cref="Launch"/>로 상태만 초기화한다.
/// 충돌·수명 종료·경기장 이탈 시 파괴하지 않고 풀로 돌아간다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class EnemyProjectile : MonoBehaviour
{
    /// <summary>배경·캐릭터보다 위에 그려 탄이 확실히 보이게 한다.</summary>
    public const int SortingOrder = 20;

    private EnemyProjectilePool pool;
    private Rigidbody2D body;
    private CircleCollider2D circle;
    private SpriteRenderer visual;
    private Transform visualTransform;

    private int damage;
    private float expireTime;
    private bool consumed;

    /// <summary>
    /// 풀이 준비 단계에서 호출한다. 여기서 만든 컴포넌트는 전투 내내 재사용된다.
    /// </summary>
    public static EnemyProjectile CreatePooled(Transform parent, EnemyProjectilePool owner)
    {
        GameObject go = new GameObject("EnemyProjectile");
        go.transform.SetParent(parent, false);

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;

        // 콜라이더 크기에 영향을 주지 않도록 그림은 자식에서 확대한다.
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Circle;
        sr.sortingOrder = SortingOrder;

        EnemyProjectile projectile = go.AddComponent<EnemyProjectile>();
        projectile.pool = owner;
        projectile.body = body;
        projectile.circle = circle;
        projectile.visual = sr;
        projectile.visualTransform = visual.transform;

        go.SetActive(false);
        return projectile;
    }

    /// <summary>빌려 온 투사체를 발사한다. 이전 상태는 전부 덮어쓴다.</summary>
    public void Launch(Vector2 position, Vector2 direction, float speed, int damageAmount,
                       float lifetime, float radius, Color color)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

        transform.position = position;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);

        damage = damageAmount;
        consumed = false;
        expireTime = Time.time + lifetime;

        circle.radius = radius;
        circle.enabled = true;
        visualTransform.localScale = Vector3.one * (radius * 2f);
        visual.color = color;

        gameObject.SetActive(true);
        // 속도는 활성화한 뒤에 넣어야 물리에 반영된다.
        body.linearVelocity = dir * speed;
    }

    /// <summary>수명 종료와 경기장 이탈을 확인한다.</summary>
    private void Update()
    {
        if (Time.time >= expireTime) { Deactivate(); return; }

        if (pool == null) return;
        Vector2 offset = (Vector2)transform.position - pool.ArenaCenter;
        if (Mathf.Abs(offset.x) > pool.ArenaHalfSize.x || Mathf.Abs(offset.y) > pool.ArenaHalfSize.y)
            Deactivate();
    }

    /// <summary>날아가던 투사체를 즉시 멈추고 풀로 돌려보낸다.</summary>
    public void Deactivate()
    {
        if (!gameObject.activeSelf) return;

        consumed = true;
        body.linearVelocity = Vector2.zero;
        circle.enabled = false;
        gameObject.SetActive(false);
        if (pool != null) pool.Return(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;

        // 발사한 보스와 다른 적은 통과한다. 벽 판정보다 먼저 걸러야
        // 보스 몸에 닿자마자 사라지는 일이 없다.
        if (other.GetComponentInParent<EnemyController>() != null) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            Health playerHealth = player.GetComponent<Health>();
            // 무적 시간 중이면 탄을 소비하지 않고 지나가게 둔다.
            if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return;

            playerHealth.TakeDamage(damage);
            Deactivate();
            return;
        }

        // 벽 등 단단한 물체에서 소멸. 출구 트리거나 다른 투사체는 통과.
        if (!other.isTrigger) Deactivate();
    }
}
