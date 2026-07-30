using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아쿠스타의 기하학 레이저 — 3층 일반 적 중 주요 직접 피해 담당. CC는 쓰지 않는다.
///
/// 전투장 외곽의 지점을 예고하고 그리로 순간이동한 뒤, 자신을 중심으로 <c>+</c> 또는
/// <c>×</c> 모양의 레이저 경로를 예고하고 네 방향으로 동시에 발사한다. 발사 후에는
/// 잠시 정지해 공격받을 기회를 주고, 다음 공격에서는 반드시 다른 모양을 쓴다.
///
/// 순간이동 직후 바로 쏘지 않는다 — 레이저 예고 시간이 항상 통째로 보장된다.
/// 예고선과 실제 판정은 위치·굵기가 같다.
/// </summary>
public class EnemyStarLaserAbility : EnemyAbility
{
    [Header("순간이동")]
    [Tooltip("전투장 중심(부모 방) 기준 이동 후보 범위의 반너비·반높이. 가장자리 안쪽 링이다. " +
             "몸이 놓일 자리라 벽 안쪽 면(±7 · ±5)에서 RoomArena.BodyMargin만큼 들인 값을 쓴다 — " +
             "더 좁게 잡으면 레이저 십자가 벽 근처를 지나가지 않아 가장자리가 안전해진다.")]
    [SerializeField] private Vector2 arenaHalf = new Vector2(6.5f, 4.5f);
    [Tooltip("가장자리에서 이만큼 안쪽의 띠에서 지점을 고른다.")]
    [SerializeField, Min(0f)] private float edgeInset = 1.1f;
    [Tooltip("플레이어와 이 거리 이상 떨어진 지점만 고른다 — 코앞 순간이동은 반칙 같다.")]
    [SerializeField, Min(0f)] private float minTeleportDistance = 2.6f;
    [SerializeField, Min(0.05f)] private float teleportTelegraph = 0.32f;

    [Header("레이저")]
    [SerializeField, Min(0.1f)] private float laserLength = 13f;
    [SerializeField, Min(0.05f)] private float laserWidth = 0.55f;
    [Tooltip("순간이동 후 레이저를 예고하는 시간. 어떤 경우에도 줄지 않는다.")]
    [SerializeField, Min(0.05f)] private float laserTelegraph = 0.55f;
    [Tooltip("레이저가 켜져 피해 판정을 유지하는 시간.")]
    [SerializeField, Min(0.05f)] private float laserDuration = 0.4f;
    [SerializeField, Min(0)] private int laserDamage = 18;
    [Tooltip("발사 중 Idle 동작을 이만큼 배속해 회전을 빠르게 보여 준다.")]
    [SerializeField, Min(1f)] private float spinSpeedMultiplier = 2.6f;
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.5f);
    [SerializeField] private Color beamColor = new Color(0.75f, 0.4f, 1f, 0.9f);

    [Header("후딜")]
    [Tooltip("발사 후 정지해 공격받을 기회를 주는 시간.")]
    [SerializeField, Min(0f)] private float recovery = 0.7f;

    /// <summary>직전에 × 모양을 썼는지. 매번 반대 모양을 쓴다.</summary>
    private bool lastWasDiagonal;

    private Vector2 ArenaCenter => transform.parent != null
        ? (Vector2)transform.parent.position : (Vector2)transform.position;

    protected override IEnumerator Perform()
    {
        // 1. 외곽 지점을 골라 예고하고 순간이동한다.
        Vector2 destination = PickEdgePoint();
        AttackTelegraph marker = AttackTelegraph.CreateRing(
            EffectRoot, destination, 0.7f, warningColor);
        marker.Pulse(teleportTelegraph);
        // 예고하는 동안 제자리에 붙들어 둔다. 시전 직전까지 걷던 관성이나 다른 적에게
        // 밀린 힘이 남아 있으면, 예고 원은 여기 그렸는데 몸은 저기 가 있게 된다.
        yield return HoldStill(teleportTelegraph);
        if (Health.IsDead) yield break;

        Body.position = destination;
        transform.position = destination;

        // 2. 이번 모양을 정한다 — 직전과 반드시 다르게.
        lastWasDiagonal = !lastWasDiagonal;
        bool diagonal = lastWasDiagonal;
        Vector2[] directions = diagonal
            ? new[] { new Vector2(1f, 1f).normalized, new Vector2(-1f, 1f).normalized,
                      new Vector2(-1f, -1f).normalized, new Vector2(1f, -1f).normalized }
            : new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down };

        // 3. 네 갈래 예고. 순간이동 직후라도 이 시간은 통째로 보장한다.
        //    원점은 여기서 한 번 정해 예고·빔·판정이 전부 같은 값을 쓴다. 매번 Body.position을
        //    다시 읽으면, 예고하는 동안 몸이 한 뼘이라도 밀렸을 때 빔이 예고선을 벗어난다.
        Vector2 origin = destination;
        foreach (Vector2 direction in directions)
        {
            AttackTelegraph line = AttackTelegraph.CreateLine(
                EffectRoot, origin, direction, laserLength, laserWidth, warningColor);
            line.Pulse(laserTelegraph);
        }
        yield return HoldStill(laserTelegraph);
        if (Health.IsDead) yield break;

        // 4. 동시 발사. Idle을 배속해 빙글 도는 것으로 발사를 표현한다.
        PlayAction("Idle", Vector2.down);
        Animator animator = GetComponent<Animator>();
        float normalSpeed = animator != null ? animator.speed : 1f;
        if (animator != null) animator.speed = normalSpeed * spinSpeedMultiplier;

        var beams = new List<SpriteRenderer>(4);
        foreach (Vector2 direction in directions)
            beams.Add(CreateBeam(origin, direction));

        float damageEnd = Time.time + laserDuration;
        while (Time.time < damageEnd && !Health.IsDead)
        {
            // 쏘는 동안에도 원점에 못 박는다. 빔은 이미 그려져 움직이지 않으므로,
            // 몸만 밀려나면 그림과 판정이 어긋난다.
            Body.linearVelocity = Vector2.zero;
            Body.position = origin;
            TryDamage(origin, directions);
            yield return null;
        }

        foreach (SpriteRenderer beam in beams)
            if (beam != null) Destroy(beam.gameObject);
        if (animator != null) animator.speed = normalSpeed;
        if (Health.IsDead) yield break;

        // 5. 정지 — 공격받을 기회.
        float recoverEnd = Time.time + recovery;
        while (Time.time < recoverEnd && !Health.IsDead)
        {
            HoldPosition();
            yield return null;
        }
    }

    private Vector2 PickEdgePoint()
    {
        Vector2 center = ArenaCenter;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            // 가장자리 띠: 네 변 중 하나에 붙인다.
            int side = Random.Range(0, 4);
            float x = side == 0 ? -arenaHalf.x + Random.Range(0f, edgeInset)
                    : side == 1 ? arenaHalf.x - Random.Range(0f, edgeInset)
                    : Random.Range(-arenaHalf.x + edgeInset, arenaHalf.x - edgeInset);
            float y = side == 2 ? -arenaHalf.y + Random.Range(0f, edgeInset)
                    : side == 3 ? arenaHalf.y - Random.Range(0f, edgeInset)
                    : Random.Range(-arenaHalf.y + edgeInset, arenaHalf.y - edgeInset);
            Vector2 candidate = center + new Vector2(x, y);
            if (Vector2.Distance(candidate, PlayerPosition) >= minTeleportDistance)
                return candidate;
        }
        // 다 실패하면 플레이어 반대편 구석으로 간다.
        Vector2 away = ((Vector2)transform.position - PlayerPosition).normalized;
        return center + Vector2.Scale(away, arenaHalf - Vector2.one * edgeInset);
    }

    /// <summary>예고하는 동안 몸을 제자리에 못 박는다. 예고 위치와 실제 위치를 같게 유지한다.</summary>
    private IEnumerator HoldStill(float seconds)
    {
        Vector2 anchor = Body.position;
        float end = Time.time + seconds;
        while (Time.time < end && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            Body.position = anchor;
            yield return null;
        }
    }

    private SpriteRenderer CreateBeam(Vector2 origin, Vector2 direction)
    {
        GameObject go = EnemyEffect.Mark(new GameObject("StarLaser"));
        go.transform.SetParent(EffectRoot, false);
        go.transform.position = origin + direction * (laserLength * 0.5f);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        go.transform.localScale = new Vector3(laserLength, laserWidth, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Square;
        sr.color = beamColor;
        sr.sortingOrder = 12; // 캐릭터(10)보다 앞
        return sr;
    }

    private void TryDamage(Vector2 origin, Vector2[] directions)
    {
        if (PlayerHealth == null || PlayerHealth.IsDead || PlayerHealth.IsInvincible) return;
        Vector2 offset = PlayerPosition - origin;
        float halfWidth = laserWidth * 0.5f;

        foreach (Vector2 direction in directions)
        {
            float along = Vector2.Dot(offset, direction);
            if (along < 0f || along > laserLength) continue;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            if (Mathf.Abs(Vector2.Dot(offset, perpendicular)) > halfWidth) continue;
            PlayerHealth.TakeDamage(laserDamage);
            return;
        }
    }
}
