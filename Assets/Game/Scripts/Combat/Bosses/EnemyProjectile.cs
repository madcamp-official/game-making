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
    /// <param name="shape">그림. 비우면 동그란 탄이다. 고지의 가시는 삼각형을 쓴다.</param>
    /// <param name="stretch">
    /// 나아가는 방향으로 그림만 늘리는 배율. <b>판정은 늘어나지 않는다</b> —
    /// 콜라이더는 <paramref name="radius"/>짜리 원 그대로다. 가시처럼 길쭉해 보여야 하는
    /// 탄을 위한 값이고, 판정까지 늘리면 "스치지도 않았는데 맞았다"가 된다.
    /// </param>
    public void Launch(Vector2 position, Vector2 direction, float speed, int damageAmount,
                       float lifetime, float radius, Color color,
                       Sprite shape = null, float stretch = 1f)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

        transform.position = position;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);

        damage = damageAmount;
        consumed = false;
        expireTime = Time.time + lifetime;

        circle.radius = radius;
        circle.enabled = true;
        // 풀에서 빌려 쓰므로 그림도 매번 다시 정해 준다. 앞서 쓴 탄의 모양이 남으면 안 된다.
        visual.sprite = shape != null ? shape : PrimitiveSprites.Circle;
        visualTransform.localScale = new Vector3(radius * 2f * Mathf.Max(0.01f, stretch), radius * 2f, 1f);
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
