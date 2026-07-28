using System.Collections;
using UnityEngine;

/// <summary>
/// 나인테일의 공격. 예고선을 띄운 뒤 입에서 긴 화염 줄기를 뿜는다.
/// 불은 입에서 먼 곳으로 물결처럼 번지고, 바닥에 한동안 남아 그 선을 밟을 수 없게 한다.
///
/// 한 방의 피해보다 <b>이동 방향을 제한하는 것</b>이 목적이다. 불줄기가 남아 있는 동안
/// 플레이어는 돌아가야 하고, 그 사이 다른 적이 붙는다. 시전 중에는 Shoot 동작을
/// 마지막 프레임으로 굳혀 계속 뿜는 자세를 유지한다.
/// </summary>
public class EnemyFlameLineAbility : EnemyAbility
{
    [Header("화염 줄기")]
    [SerializeField, Min(0f)] private float windup = 0.55f;
    [SerializeField, Min(1f)] private float flameLength = 6.5f;
    [Tooltip("불덩이 하나의 반지름. 줄기의 굵기가 된다.")]
    [SerializeField, Min(0.1f)] private float flameRadius = 0.5f;
    [Tooltip("불덩이 사이 간격. 반지름보다 크면 줄기에 빈틈이 생긴다.")]
    [SerializeField, Min(0.1f)] private float flameSpacing = 0.7f;
    [Tooltip("불이 바닥에 남는 시간. 길수록 길이 오래 막힌다.")]
    [SerializeField, Min(0.2f)] private float flameDuration = 1.6f;
    [SerializeField, Min(0)] private int damage = 12;
    [SerializeField, Min(0.05f)] private float tickInterval = 0.45f;
    [Tooltip("불이 입에서 끝까지 번지는 데 걸리는 시간.")]
    [SerializeField, Min(0f)] private float spreadTime = 0.35f;
    [SerializeField, Min(0f)] private float recovery = 0.8f;

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.35f);
    [SerializeField] private Color flameColor = new Color(1f, 0.45f, 0.1f, 0.65f);

    private const float MuzzleOffset = 0.5f;

    protected override IEnumerator Perform()
    {
        Vector2 origin = transform.position;
        Vector2 aim = DirectionToPlayer;

        AttackTelegraph telegraph = AttackTelegraph.CreateLine(
            EffectRoot, origin, aim, flameLength, flameRadius * 2f, warningColor);
        telegraph.Pulse(windup);
        // Shoot이 한 번 재생되고 뿜는 자세로 굳는다. 예고가 끝날 즈음 동작도 끝난다.
        PlayAction("Shoot", aim);

        yield return new WaitForSeconds(windup);
        if (Health.IsDead) yield break;

        // 입에서 먼 곳으로 번지는 물결. 전부 한꺼번에 깔리면 불이 아니라 벽처럼 보인다.
        Vector2 muzzle = (Vector2)transform.position + aim * MuzzleOffset;
        int count = Mathf.Max(1, Mathf.CeilToInt((flameLength - MuzzleOffset) / flameSpacing));
        float perFlame = spreadTime / count;
        for (int i = 0; i < count; i++)
        {
            if (Health.IsDead) yield break;
            DamageZone.Spawn(EffectRoot, muzzle + aim * (i * flameSpacing),
                             flameRadius, flameDuration, damage, tickInterval, flameColor);
            yield return new WaitForSeconds(perFlame);
        }

        // 불이 다 번진 뒤에도 잠깐 자세를 유지해 "뿜었다"가 읽히게 한다.
        yield return new WaitForSeconds(0.35f);
        StopAction();
        yield return new WaitForSeconds(recovery);
    }
}
