using System.Collections;
using UnityEngine;

/// <summary>
/// 스라크의 공격. 돌진할 선을 미리 그려 보여 준 뒤 그 선을 따라 몸을 던진다.
///
/// 예고한 방향으로만 달린다 — 달리는 도중에 플레이어를 다시 쫓지 않는다.
/// 그래야 예고선 밖으로 비키는 것이 정답이 된다.
/// 벽에 부딪히면 그 자리에서 멈춘다.
/// </summary>
public class EnemyDashAbility : EnemyAbility
{
    [Header("돌진")]
    [SerializeField, Min(0f)] private float windup = 0.7f;
    [SerializeField, Min(0f)] private float dashSpeed = 11f;
    [Tooltip("돌진 거리. 벽에 막히면 더 짧아진다.")]
    [SerializeField, Min(0f)] private float dashDistance = 5.5f;
    [SerializeField, Min(0)] private int damage = 14;
    [Tooltip("돌진이 끝난 뒤 숨을 고르는 시간. 이때가 반격할 틈이다.")]
    [SerializeField, Min(0f)] private float recovery = 0.6f;
    [Tooltip("돌진 판정 반지름. 스라크 몸집보다 조금 크게 잡는다.")]
    [SerializeField, Min(0f)] private float hitRadius = 0.55f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.3f, 0.45f);
    [Tooltip("돌진하는 동안 몸에 입히는 색.")]
    [SerializeField] private Color dashTint = new Color(1f, 0.6f, 0.6f, 1f);

    private const float TelegraphWidth = 0.9f;
    private const float StallWindow = 0.15f;

    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    protected override IEnumerator Perform()
    {
        Vector2 origin = transform.position;
        Vector2 direction = DirectionToPlayer;

        AttackTelegraph telegraph = AttackTelegraph.CreateLine(
            EffectRoot, origin, direction, dashDistance, TelegraphWidth, warningColor);
        telegraph.Pulse(windup);
        PlayAction("Charge", direction);

        yield return new WaitForSeconds(windup);
        if (Health.IsDead) yield break;

        // 돌진하는 동안에도 예고한 방향을 계속 보게 둔다 (Walk로 돌아가되 방향은 고정).
        PlayAction("Walk", direction);
        yield return Dash(direction);
        StopAction();

        Body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(recovery);
    }

    private IEnumerator Dash(Vector2 direction)
    {
        SetTint(dashTint);

        float duration = dashDistance / Mathf.Max(0.01f, dashSpeed);
        float elapsed = 0f;
        bool hit = false;

        // 막혔는지는 한 프레임이 아니라 일정 시간 동안 얼마나 나아갔는지로 본다.
        // 속도는 Update에서 넣고 실제 이동은 FixedUpdate에서 일어나기 때문에,
        // 프레임 단위로 재면 멀쩡히 달리는 중에도 "안 움직였다"가 자주 나온다.
        Vector2 checkpoint = transform.position;
        float sinceCheckpoint = 0f;

        while (elapsed < duration)
        {
            if (Health.IsDead) break;

            Body.linearVelocity = direction * dashSpeed;
            elapsed += Time.deltaTime;
            sinceCheckpoint += Time.deltaTime;

            // 돌진 한 번에 한 번만 때린다. 스치기만 해도 여러 번 맞으면 즉사한다.
            if (!hit && TryHitPlayer()) hit = true;

            if (sinceCheckpoint >= StallWindow)
            {
                // 벽에 막혀 더 나아가지 못하면 남은 시간을 버린다.
                if (Vector2.Distance(checkpoint, transform.position) < dashSpeed * StallWindow * 0.25f) break;
                checkpoint = transform.position;
                sinceCheckpoint = 0f;
            }

            yield return null;
        }

        Body.linearVelocity = Vector2.zero;
        SetTint(Color.white);
    }

    private bool TryHitPlayer()
    {
        if (PlayerHealth == null || PlayerHealth.IsDead || PlayerHealth.IsInvincible) return false;
        if (Vector2.Distance(transform.position, PlayerPosition) > hitRadius) return false;

        PlayerHealth.TakeDamage(damage);
        return true;
    }

    private void SetTint(Color color)
    {
        if (spriteRenderer != null) spriteRenderer.color = color;
    }
}
