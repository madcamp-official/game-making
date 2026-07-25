using UnityEngine;

/// <summary>
/// 직선 투사체. 적 또는 벽에 닿으면 사라지고, 관통하지 않는다.
/// 피해량은 발사자가 Launch로 지정한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 12f;
    [SerializeField, Min(0f)] private float lifetime = 1.2f;

    private int damage;
    private bool consumed;

    public void Launch(Vector2 direction, int damageAmount)
    {
        damage = damageAmount;
        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        GetComponent<Rigidbody2D>().linearVelocity = dir * speed;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        // 발사자(플레이어)는 무시
        if (other.GetComponentInParent<PlayerController>() != null) return;

        if (other.GetComponentInParent<EnemyController>() != null)
        {
            Health enemyHealth = other.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                consumed = true;
                enemyHealth.TakeDamage(damage);
                Destroy(gameObject);
            }
            return;
        }

        // 벽 등 단단한 물체에 충돌하면 소멸. 출구·상점 등 트리거는 통과.
        if (!other.isTrigger)
        {
            consumed = true;
            Destroy(gameObject);
        }
    }
}
