using UnityEngine;

/// <summary>
/// 적이 쏘는 직선 투사체. 플레이어에게만 피해를 준다.
///
/// 플레이어의 <see cref="Projectile"/>과 대칭되는 반대편 구현이다. 발사자와 다른 적은
/// 통과하고, 벽 같은 단단한 물체에 닿거나 수명이 다하면 사라진다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class EnemyProjectile : MonoBehaviour
{
    /// <summary>배경·캐릭터보다 위에 그려 탄이 확실히 보이게 한다.</summary>
    public const int SortingOrder = 20;

    private int damage;
    private bool consumed;

    /// <summary>
    /// 투사체를 만들어 바로 발사한다. <paramref name="parent"/>는 배율이 1인 오브젝트여야
    /// 콜라이더 반지름이 의도한 크기로 유지된다.
    /// </summary>
    public static EnemyProjectile Spawn(Transform parent, Vector2 position, Vector2 direction,
                                        float speed, int damage, float lifetime, float radius, Color color)
    {
        GameObject go = new GameObject("EnemyProjectile");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = radius;

        // 콜라이더 크기에 영향을 주지 않도록 그림은 자식에서 확대한다.
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * (radius * 2f);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Circle;
        sr.color = color;
        sr.sortingOrder = SortingOrder;

        EnemyProjectile projectile = go.AddComponent<EnemyProjectile>();
        projectile.damage = damage;

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        body.linearVelocity = dir * speed;
        Destroy(go, lifetime);
        return projectile;
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

            consumed = true;
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 벽 등 단단한 물체에서 소멸. 출구 트리거나 다른 투사체는 통과.
        if (!other.isTrigger)
        {
            consumed = true;
            Destroy(gameObject);
        }
    }
}
