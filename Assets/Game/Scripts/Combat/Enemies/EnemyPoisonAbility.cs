using System.Collections;
using UnityEngine;

/// <summary>
/// 콘팡의 공격. 버터플의 독가루와 같은 장판을 딱 하나만 깐다.
///
/// 장판 크기·피해·틱 간격은 보스 쪽과 같은 값을 기본으로 둔다
/// (<see cref="ButterfreeBossController"/>의 1페이즈 설정). 개수만 1개로 줄인 것이
/// 잡몹판과 보스판의 차이다.
/// </summary>
public class EnemyPoisonAbility : EnemyAbility
{
    [Header("장판")]
    [Tooltip("버터플 1페이즈와 같은 크기.")]
    [SerializeField, Min(0f)] private float radius = 1.26f;
    [Tooltip("예고 원이 뜬 뒤 장판으로 바뀌기까지의 시간.")]
    [SerializeField, Min(0f)] private float windup = 0.7f;
    [SerializeField, Min(0f)] private float duration = 5f;
    [SerializeField, Min(0)] private int damage = 8;
    [SerializeField, Min(0f)] private float tickInterval = 1f;
    [Tooltip("플레이어가 이동하는 앞을 얼마나 내다볼지. 0이면 발밑에 깐다.")]
    [SerializeField, Min(0f)] private float predictLead = 0.6f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.75f, 0.35f, 0.85f, 0.35f);
    [SerializeField] private Color zoneColor = new Color(0.45f, 0.12f, 0.6f, 0.65f);

    private Rigidbody2D playerBody;

    protected override void Start()
    {
        base.Start();
        if (Player != null) playerBody = Player.GetComponent<Rigidbody2D>();
    }

    protected override IEnumerator Perform()
    {
        // 발밑에 그대로 깔면 예고를 보고 한 걸음만 옮겨도 100% 피해진다.
        // 이동 방향을 조금 내다봐서, 멈추거나 방향을 꺾게 만든다.
        Vector2 target = PlayerPosition;
        if (playerBody != null) target += playerBody.linearVelocity * predictLead;

        AttackTelegraph telegraph = AttackTelegraph.CreateCircle(EffectRoot, target, radius, warningColor);
        telegraph.Pulse(windup);
        PlayAction("Charge", target - (Vector2)transform.position);

        yield return new WaitForSeconds(windup);
        StopAction();
        if (Health.IsDead) yield break;

        DamageZone.Spawn(EffectRoot, target, radius, duration, damage, tickInterval, zoneColor);
    }
}
