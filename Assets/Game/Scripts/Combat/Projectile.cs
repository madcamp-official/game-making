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

    /// <summary>
    /// 속도와 사거리를 지정해 쏜다 (불꽃세례). 사거리는 수명으로 환산한다 —
    /// 거리를 재며 날리는 것보다 싸고, 벽·적 충돌 소멸은 어차피 트리거가 맡는다.
    /// </summary>
    public void Launch(Vector2 direction, int damageAmount, float speedOverride, float maxRange)
    {
        if (speedOverride > 0f) speed = speedOverride;
        if (maxRange > 0f) lifetime = maxRange / Mathf.Max(0.01f, speed);
        Launch(direction, damageAmount);
    }

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
                PlayerRelicEffects.ReportDamageDealt(damage);
                GameAudio.PlayPlayerHit();
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
