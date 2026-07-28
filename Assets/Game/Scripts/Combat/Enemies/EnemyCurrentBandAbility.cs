using System.Collections;
using UnityEngine;

/// <summary>
/// 신뇽의 해류 부리기 — 마지막 일반 전투방에 한 마리만 나오는 엘리트.
///
/// 전투장을 가로지르는 가로 또는 세로 해류 띠 하나를 골라, 범위와 미는 방향을
/// 화살표로 예고한 뒤(Charge) 일정 시간 활성화한다. 띠가 끝나면 쿨다운을 거쳐
/// 위치와 방향을 바꿔 다시 깐다. 동시에 존재하는 해류는 언제나 한 줄이다.
///
/// 띠 자체(<see cref="CurrentBand"/>)는 신뇽과 별개로 살아 움직인다 — 신뇽은 예고와
/// 설치만 하고 곧바로 평소 이동으로 돌아가므로, 띠가 흐르는 동안에도 얌전히 서
/// 있지 않는다. 신뇽이 죽으면 띠도 곧 스스로 걷힌다.
/// </summary>
public class EnemyCurrentBandAbility : EnemyAbility
{
    [Header("해류 띠")]
    [Tooltip("전투장 중심(부모 방) 기준 반너비·반높이. 띠는 이 영역을 끝까지 가로지른다.")]
    [SerializeField] private Vector2 arenaHalf = new Vector2(5.8f, 3.8f);
    [SerializeField, Min(0.5f)] private float bandThickness = 2.4f;
    [Tooltip("띠 안의 플레이어를 미는 속도. 플레이어(5)보다 느려 거슬러 걸을 수 있다.")]
    [SerializeField, Min(0f)] private float pushSpeed = 2.4f;
    [SerializeField, Min(0.05f)] private float telegraph = 0.8f;
    [SerializeField, Min(0.5f)] private float bandDuration = 4f;
    [SerializeField] private Color bandColor = new Color(0.35f, 0.7f, 1f, 0.28f);
    [SerializeField] private Color arrowColor = new Color(0.08f, 0.28f, 0.62f, 0.8f);

    private CurrentBand activeBand;
    /// <summary>직전 띠가 가로였는지. 같은 모양이 이어지지 않게 번갈아 쓴다.</summary>
    private bool lastHorizontal;

    private Vector2 ArenaCenter => transform.parent != null
        ? (Vector2)transform.parent.position : (Vector2)transform.position;

    protected override IEnumerator Perform()
    {
        // 이전 띠가 아직 걷히는 중이면 겹치지 않게 기다린다 — 해류는 언제나 한 줄이다.
        while (activeBand != null && !Health.IsDead)
            yield return null;
        if (Health.IsDead) yield break;

        // 1. 띠를 고른다. 가로/세로를 번갈아, 위치는 플레이어가 지금 서 있는 줄로.
        //    "지금 안전한 곳"이 위험해지는 쪽이 해류를 읽게 만든다.
        lastHorizontal = !lastHorizontal;
        bool horizontal = lastHorizontal;
        Vector2 center = ArenaCenter;

        Rect area;
        Vector2 push;
        if (horizontal)
        {
            float y = Mathf.Clamp(PlayerPosition.y, center.y - arenaHalf.y + bandThickness * 0.5f,
                                                    center.y + arenaHalf.y - bandThickness * 0.5f);
            area = new Rect(center.x - arenaHalf.x, y - bandThickness * 0.5f,
                            arenaHalf.x * 2f, bandThickness);
            push = Random.value < 0.5f ? Vector2.right : Vector2.left;
        }
        else
        {
            float x = Mathf.Clamp(PlayerPosition.x, center.x - arenaHalf.x + bandThickness * 0.5f,
                                                    center.x + arenaHalf.x - bandThickness * 0.5f);
            area = new Rect(x - bandThickness * 0.5f, center.y - arenaHalf.y,
                            bandThickness, arenaHalf.y * 2f);
            push = Random.value < 0.5f ? Vector2.up : Vector2.down;
        }

        // 2. 예고 — 범위 사각형과 미는 방향 화살표. 신뇽은 그동안 힘을 모은다(Charge).
        PlayAction("Charge", push);
        AttackTelegraph warning = AttackTelegraph.CreateLine(
            EffectRoot, horizontal ? new Vector2(area.xMin, area.center.y)
                                   : new Vector2(area.center.x, area.yMin),
            horizontal ? Vector2.right : Vector2.up,
            horizontal ? area.width : area.height, bandThickness, bandColor);
        warning.Pulse(telegraph);

        AttackTelegraph arrowHint = AttackTelegraph.CreateTriangle(
            EffectRoot, area.center - push * (bandThickness * 0.6f), push,
            bandThickness * 1.2f, bandThickness * 0.9f, arrowColor);
        arrowHint.Pulse(telegraph);

        float telegraphEnd = Time.time + telegraph;
        while (Time.time < telegraphEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
        if (Health.IsDead) yield break;

        // 3. 활성화. 띠는 스스로 굴러가고 신뇽은 곧 평소 이동으로 돌아간다.
        activeBand = CurrentBand.Spawn(EffectRoot, area, push, pushSpeed, bandDuration,
                                       Health, bandColor, arrowColor);
        yield return new WaitForSeconds(0.2f);
    }
}
