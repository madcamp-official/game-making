using System.Collections;
using UnityEngine;

/// <summary>
/// 신뇽의 해류 부리기 — 마지막 일반 전투방에 한 마리만 나오는 엘리트.
///
/// <b>전투장 전체</b>를 한 방향으로 흐르게 만든다. 미는 방향을 화살표로 예고한 뒤(Charge)
/// 일정 시간 활성화하고, 끝나면 쿨다운을 거쳐 방향을 바꿔 다시 건다.
///
/// 갸라도스의 삼중 해류와 일부러 다르게 뒀다. 그쪽은 수로마다 방향이 갈려서 "어느 줄에
/// 설 것인가"를 묻지만, 신뇽은 맵 전체가 <b>한 방향</b>이라 피할 줄 자체가 없다 — 대신
/// 읽을 것이 하나뿐이라 규칙은 더 단순하다. 미는 속도가 플레이어보다 느리므로 거슬러
/// 걸을 수 있고, 해류가 바꾸는 것은 갈 수 있는 곳이 아니라 가는 데 드는 시간이다.
///
/// 해류 자체(<see cref="CurrentBand"/>)는 신뇽과 별개로 살아 움직인다 — 신뇽은 예고와
/// 설치만 하고 곧바로 평소 이동으로 돌아가므로, 흐르는 동안에도 얌전히 서 있지 않는다.
/// 신뇽이 죽으면 해류도 곧 스스로 걷힌다.
/// </summary>
public class EnemyCurrentBandAbility : EnemyAbility
{
    [Header("해류")]
    [Tooltip("전투장 중심(부모 방) 기준 반너비·반높이. 해류는 이 영역 전체를 덮는다. " +
             "벽 안쪽 면과 맞춰야 한다 — 좁으면 가장자리에 밀리지 않는 안전지대가 남는다.")]
    [SerializeField] private Vector2 arenaHalf = new Vector2(7f, 5f);
    [Tooltip("예고에 띄우는 방향 화살표의 길이.")]
    [SerializeField, Min(0.5f)] private float telegraphArrowSize = 2.4f;
    [Tooltip("해류 안의 플레이어를 미는 속도. 플레이어(5)보다 느려 거슬러 걸을 수 있다.")]
    [SerializeField, Min(0f)] private float pushSpeed = 3.2f;
    [SerializeField, Min(0.05f)] private float telegraph = 0.55f;
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

        // 1. 방향만 고른다. 범위는 언제나 전투장 전체다.
        //    가로·세로를 번갈아 써서 같은 축이 연달아 나오지 않게 하고, 미는 쪽은 그 안에서 뽑는다.
        lastHorizontal = !lastHorizontal;
        bool horizontal = lastHorizontal;
        Vector2 center = ArenaCenter;

        Rect area = new Rect(center - arenaHalf, arenaHalf * 2f);
        Vector2 push = horizontal
            ? (Random.value < 0.5f ? Vector2.right : Vector2.left)
            : (Random.value < 0.5f ? Vector2.up : Vector2.down);

        // 2. 예고 — 전투장 전체를 덮는 사각형과 미는 방향 화살표. 신뇽은 그동안 힘을 모은다(Charge).
        //    가로로 흐르면 폭이 곧 맵 높이가 되어, 예고 사각형이 방을 통째로 덮는다.
        PlayAction("Charge", push);
        AttackTelegraph warning = AttackTelegraph.CreateLine(
            EffectRoot, horizontal ? new Vector2(area.xMin, area.center.y)
                                   : new Vector2(area.center.x, area.yMin),
            horizontal ? Vector2.right : Vector2.up,
            horizontal ? area.width : area.height,
            horizontal ? area.height : area.width, bandColor);
        warning.Pulse(telegraph);

        AttackTelegraph arrowHint = AttackTelegraph.CreateTriangle(
            EffectRoot, area.center - push * (telegraphArrowSize * 0.5f), push,
            telegraphArrowSize, telegraphArrowSize * 0.75f, arrowColor);
        arrowHint.Pulse(telegraph);

        float telegraphEnd = Time.time + telegraph;
        while (Time.time < telegraphEnd && !Health.IsDead)
        {
            HoldPosition();
            yield return null;
        }
        if (Health.IsDead) yield break;

        // 3. 활성화. 띠는 스스로 굴러가고 신뇽은 곧 평소 이동으로 돌아간다.
        activeBand = CurrentBand.Spawn(EffectRoot, area, push, pushSpeed, bandDuration,
                                       Health, bandColor, arrowColor);
        yield return new WaitForSeconds(0.2f);
    }
}
