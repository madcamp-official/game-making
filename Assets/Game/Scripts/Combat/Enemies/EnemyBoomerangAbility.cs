using System.Collections;
using UnityEngine;

/// <summary>
/// 텅구리의 공격. 예고선을 띄운 뒤 뼈다귀를 던진다. 뼈는 정해진 거리까지 날아갔다가
/// 던진 텅구리에게 되돌아오며, <b>나갈 때와 돌아올 때 각각 한 번씩</b> 맞을 수 있다.
///
/// 던지는 순간은 Shoot 동작의 타격 프레임(AnimData HitFrame)에 맞추고, 뼈가 돌아올 때까지
/// 던진 자세(마지막 프레임)로 굳어 기다린다 — 클립이 반복 없음이라 저절로 멈춰 준다.
/// 첫 궤도를 피하고 안심하는 순간 등 뒤로 돌아오는 뼈에 맞는 것이 이 적의 수업이다.
/// </summary>
public class EnemyBoomerangAbility : EnemyAbility
{
    [Header("던지기")]
    [SerializeField, Min(0f)] private float windup = 0.4f;
    [Tooltip("Shoot 동작이 시작되고 뼈가 손을 떠나기까지의 시간. 타격 프레임에 맞춘 값이다.")]
    [SerializeField, Min(0f)] private float releaseDelay = 0.13f;
    [SerializeField, Min(0.5f)] private float throwDistance = 4.6f;
    [SerializeField, Min(0.5f)] private float boneSpeed = 8f;
    [SerializeField, Min(0)] private int damage = 13;
    [Tooltip("뼈 중심에서 이 거리 안이면 맞는다.")]
    [SerializeField, Min(0f)] private float hitRadius = 0.55f;
    [Tooltip("뼈를 맞히고 회수했을 때 멈추는 시간.")]
    [SerializeField, Min(0f)] private float hitRecovery = 0.7f;
    [Tooltip("나가는 뼈와 돌아오는 뼈가 모두 빗나갔을 때 멈추는 시간.")]
    [SerializeField, Min(0f)] private float missRecovery = 1.3f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.35f);
    [SerializeField] private Color boneColor = new Color(0.93f, 0.9f, 0.8f, 1f);

    private const float BoneSpinSpeed = 720f;   // 초당 회전 각도
    private const float CatchDistance = 0.35f;

    /// <summary>이번 왕복에서 한 번이라도 맞혔는지. 회수 후 후딜의 길이를 가른다.</summary>
    private bool boneConnected;

    protected override IEnumerator Perform()
    {
        Vector2 origin = transform.position;
        Vector2 aim = DirectionToPlayer;

        AttackTelegraph telegraph = AttackTelegraph.CreateLine(
            EffectRoot, origin, aim, throwDistance, hitRadius * 2f, warningColor);
        telegraph.Pulse(windup);

        yield return new WaitForSeconds(windup);
        if (Health.IsDead) yield break;

        PlayAction("Shoot", aim);
        yield return new WaitForSeconds(releaseDelay);
        if (Health.IsDead) yield break;

        yield return BoneFlight(aim);

        // 회수 자세로 정지한다. 맞혔으면 짧게, 왕복이 전부 빗나갔으면 지쳐서 길게 —
        // 빗나가게 만든 쪽에게 접근할 시간을 확실히 준다.
        StopAction();
        float pauseEnd = Time.time + (boneConnected ? hitRecovery : missRecovery);
        while (Time.time < pauseEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }

    /// <summary>뼈 하나의 왕복. 텅구리는 이 코루틴이 끝날 때까지 던진 자세로 서 있는다.</summary>
    private IEnumerator BoneFlight(Vector2 direction)
    {
        GameObject bone = EnemyEffect.Mark(new GameObject("MarowakBone"));
        // 적이 죽어도 날아가던 뼈가 공중에서 사라지지 않도록 방에 붙인다.
        bone.transform.SetParent(EffectRoot, false);
        bone.transform.position = (Vector2)transform.position + direction * 0.4f;
        bone.transform.localScale = new Vector3(0.62f, 0.2f, 1f);
        SpriteRenderer sr = bone.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Square;
        sr.color = boneColor;
        sr.sortingOrder = 12;   // 공중에 있는 것은 캐릭터보다 앞

        float traveled = 0f;
        bool hitOutbound = false;
        bool hitReturn = false;
        bool returning = false;
        boneConnected = false;
        // 던진 쪽이 죽으면 돌아갈 곳이 없다. 그때는 나가던 방향으로 소멸까지 계속 간다.
        float lifetime = Time.time + 6f;

        while (Time.time < lifetime)
        {
            float step = boneSpeed * Time.deltaTime;
            bone.transform.Rotate(0f, 0f, BoneSpinSpeed * Time.deltaTime);

            if (!returning)
            {
                bone.transform.position += (Vector3)(direction * step);
                traveled += step;
                if (traveled >= throwDistance) returning = true;
            }
            else
            {
                if (Health.IsDead) break;
                Vector2 back = (Vector2)transform.position - (Vector2)bone.transform.position;
                if (back.magnitude <= CatchDistance) break;
                bone.transform.position += (Vector3)(back.normalized * step);
            }

            // 갈 때 한 번, 올 때 한 번. 한 궤도에서 여러 번 갈리면 스치기만 해도 즉사한다.
            bool alreadyHit = returning ? hitReturn : hitOutbound;
            if (!alreadyHit && PlayerHealth != null && !PlayerHealth.IsDead && !PlayerHealth.IsInvincible &&
                Vector2.Distance(bone.transform.position, PlayerPosition) <= hitRadius)
            {
                PlayerHealth.TakeDamage(damage);
                if (returning) hitReturn = true; else hitOutbound = true;
                boneConnected = true;
            }

            yield return null;
        }

        Destroy(bone);
    }
}
