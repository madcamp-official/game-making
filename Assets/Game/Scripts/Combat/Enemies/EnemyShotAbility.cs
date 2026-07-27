using System.Collections;
using UnityEngine;

/// <summary>
/// 모다피의 공격. 플레이어 쪽을 겨눠 예고선을 띄운 뒤 직선 탄을 쏜다.
///
/// 조준은 예고가 시작되는 순간 한 번만 고정한다. 예고선이 계속 따라 돌면
/// 피할 방법이 없어서, 예고를 보고 옆으로 비키면 빗나가야 한다.
/// </summary>
public class EnemyShotAbility : EnemyAbility
{
    [Header("탄")]
    [SerializeField, Min(1)] private int count = 1;
    [Tooltip("여러 발일 때 부채꼴로 벌어지는 전체 각도.")]
    [SerializeField, Min(0f)] private float spreadAngle = 20f;
    [SerializeField, Min(0f)] private float windup = 0.55f;
    [SerializeField, Min(0f)] private float speed = 5f;
    [SerializeField, Min(0)] private int damage = 8;
    [SerializeField, Min(0f)] private float lifetime = 3f;
    [SerializeField, Min(0f)] private float projectileRadius = 0.18f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(1f, 0.9f, 0.3f, 0.4f);
    [SerializeField] private Color projectileColor = new Color(0.7f, 1f, 0.5f, 1f);

    private const float SpawnOffset = 0.45f;
    private const float TelegraphLength = 5f;

    private EnemyProjectilePool pool;

    protected override void Start()
    {
        base.Start();
        // 풀을 적이 아니라 방에 붙인다. 적과 함께 사라지면 쏘고 죽었을 때
        // 날아가던 탄이 공중에서 사라진다. 적의 배율(0.9 등)이 콜라이더에 섞이는 것도 막는다.
        pool = EnemyProjectilePool.Create(EffectRoot, Mathf.Max(4, count * 3));
    }

    protected override IEnumerator Perform()
    {
        Vector2 origin = transform.position;
        Vector2 aim = DirectionToPlayer;

        AttackTelegraph telegraph = AttackTelegraph.CreateLine(
            EffectRoot, origin, aim, TelegraphLength, projectileRadius * 2f, warningColor);
        telegraph.Pulse(windup);
        PlayAction("Charge", aim);

        yield return new WaitForSeconds(windup);
        StopAction();
        if (Health.IsDead) yield break;

        // 발사 위치는 현재 좌표로 갱신하되, 방향은 예고한 그대로 쓴다.
        Vector2 muzzle = transform.position;
        float step = count > 1 ? spreadAngle / (count - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            EnemyProjectile projectile = pool.Borrow();
            if (projectile == null) continue;

            Vector2 direction = Rotate(aim, count > 1 ? start + step * i : 0f);
            projectile.Launch(muzzle + direction * SpawnOffset, direction,
                speed, damage, lifetime, projectileRadius, projectileColor);
        }
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
