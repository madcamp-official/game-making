using System.Collections;
using UnityEngine;

/// <summary>
/// 닥트리오의 공격. 평소에는 제자리에서 Idle로 서 있다가(기본 추적 AI를 꺼 둔다),
/// 땅속으로 잠수해 플레이어 발밑까지 파고들어 예고 후 솟아오르며 때린다.
/// 땅 위에서 맞으면 반대로 플레이어에게서 <b>도망</b>친다 — 땅속으로 사라져 멀어진 뒤 다시 솟는다.
///
/// 잠수 중에는 Walk 동작에 몸이 반투명해지고, 콜라이더가 꺼져 서로 부딪히지도
/// 맞지도 않는다 — 땅속에 있으니 다른 적도 벽처럼 막지 못하고 뚫고 지나간다.
/// 그리는 순서도 캐릭터(10) 아래로 내려, 땅 위의 몸들 밑을 지나가는 것으로 보이게 한다.
/// 공격 잠수는 솟는 자리를 원으로 미리 알린다. 현재 위치와 파고드는 경로를 동시에 봐야 하는 적이다.
/// </summary>
public class EnemyBurrowAbility : EnemyAbility
{
    [Header("잠수")]
    [Tooltip("땅속 이동 속도. 플레이어(5)보다 확실히 빨라야 맞고 사라지는 것이 '도망'으로 읽힌다.")]
    [SerializeField, Min(0.5f)] private float diveSpeed = 9.5f;
    [Tooltip("파고드는 시간의 상한. 플레이어가 도망 다녀도 이 시간이 지나면 그 자리에서 솟는다.")]
    [SerializeField, Min(0.5f)] private float maxDiveTime = 2.4f;
    [Tooltip("플레이어와 이 거리 안이면 도착으로 본다.")]
    [SerializeField, Min(0f)] private float arriveDistance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float submergedAlpha = 0.45f;

    [Header("솟아오르기")]
    [Tooltip("도착해서 솟아오르기까지의 예고 시간. 이 시간이 곧 피할 시간이다.")]
    [SerializeField, Min(0.1f)] private float surfaceWindup = 0.55f;
    [Tooltip("솟아오르는 공격의 반지름. 일반 잡몹보다 넓다 — 피해도 그만큼 크다.")]
    [SerializeField, Min(0f)] private float surfaceRadius = 1.5f;
    [SerializeField, Min(0)] private int damage = 22;
    [Tooltip("공격 뒤 땅 위에 완전히 나온 채 스스로 굳는 시간. 맞혔든 빗나갔든 반드시 " +
             "이만큼 무방비다 — 높은 한 방의 값이다. 이동·잠복·공격 전부 불가.")]
    [SerializeField, Min(0f)] private float recovery = 1.5f;

    [Header("도망")]
    [Tooltip("땅 위에서 맞으면 플레이어 반대쪽으로 이만큼 파고들어 달아난다.")]
    [SerializeField, Min(0f)] private float fleeDistance = 4.5f;
    [Tooltip("달아나 솟은 뒤 잠깐 숨을 고르는 시간.")]
    [SerializeField, Min(0f)] private float fleeRecovery = 0.35f;
    [Tooltip("한 번 달아난 뒤 이 시간 안에는 다시 달아나지 않는다. 연타에 무한 도망을 막는다.")]
    [SerializeField, Min(0f)] private float fleeCooldown = 1.5f;
    [Tooltip("도망 목표점을 가두는 범위 (방 중심 기준 반너비·반높이). 콜라이더가 꺼진 채 달아나므로 " +
             "벽이 막아 주지 않는다. 벽 안쪽 면(±7 · ±5)에서 RoomArena.BodyMargin만큼 들인 값 — " +
             "벽에 겹친 채 솟으면 물리가 밀어낸다.")]
    [SerializeField] private Vector2 fleeBounds = new Vector2(6.5f, 4.5f);

    [Header("색")]
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.45f);
    [SerializeField] private Color burstColor = new Color(0.75f, 0.5f, 0.25f, 0.7f);

    /// <summary>땅속에 있는 동안의 그리기 순서. 지형·장판(1)보다 위, 캐릭터(10)보다 아래 —
    /// 다른 몸들 밑을 지나가는 것으로 보여야 겹침이 어색하지 않다.</summary>
    private const int SubmergedSortingOrder = 5;

    private SpriteRenderer spriteRenderer;
    private int surfaceSortingOrder;
    private float nextFleeTime;

    /// <summary>
    /// 지금 공격 잠수(잠수~솟아오르기)를 진행 중인 닥트리오. 한 방에 여러 마리가 있어도
    /// 이 자리가 비어야만 다음 마리가 파고들 수 있다 — 잠복 기습이 동시에 터지지 않고,
    /// 예고 원도 한 번에 하나뿐이라 같은 자리에 겹칠 일이 없다(공격 위치 예약을 겸한다).
    /// 스턴(recovery)은 토큰을 놓은 뒤라, 첫 마리가 굳어 있는 동안 다음 마리가 파고든다.
    /// </summary>
    private static EnemyBurrowAbility activeDiver;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) surfaceSortingOrder = spriteRenderer.sortingOrder;
    }

    /// <summary>다른 마리가 공격 잠수 중이면 순서를 기다린다.</summary>
    protected override bool ReadyToCast() => activeDiver == null;

    protected override IEnumerator Perform()
    {
        // 잠수. 파고드는 동안에는 살아 있는 플레이어 위치를 계속 쫓는다 —
        // 솟는 자리는 어차피 도착한 뒤의 예고가 알려 준다.
        activeDiver = this;
        PlayAction("Walk", DirectionToPlayer);
        SetSubmerged(true);

        float deadline = Time.time + maxDiveTime;
        while (Time.time < deadline && !Health.IsDead)
        {
            Vector2 toPlayer = PlayerPosition - (Vector2)transform.position;
            if (toPlayer.magnitude <= arriveDistance) break;
            Body.linearVelocity = toPlayer.normalized * diveSpeed;
            PlayAction("Walk", toPlayer);
            yield return null;
        }

        Body.linearVelocity = Vector2.zero;
        if (Health.IsDead) { SetSubmerged(false); ReleaseDive(); yield break; }

        // 예고. 잠수한 채 그 자리에서 차오른다.
        AttackTelegraph warning = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, surfaceRadius, warningColor);
        warning.Pulse(surfaceWindup);
        yield return new WaitForSeconds(surfaceWindup);

        // 솟아오르기. Idle로 다시 나타나며, 그린 원 안이면 맞는다.
        SetSubmerged(false);
        PlayAction("Idle", DirectionToPlayer);

        AttackTelegraph burst = AttackTelegraph.CreateCircle(
            EffectRoot, transform.position, surfaceRadius, burstColor);
        burst.Hold(0.18f);

        if (!Health.IsDead && PlayerHealth != null && !PlayerHealth.IsDead &&
            Vector2.Distance(transform.position, PlayerPosition) <= surfaceRadius + 0.3f)
            PlayerHealth.TakeDamage(damage);

        // 공격이 끝났으니 다음 마리는 파고들어도 된다 — 스턴은 이 마리 혼자 치른다.
        ReleaseDive();

        // 맞혔든 빗나갔든 반드시 굳는다. 이동·잠복·공격 전부 불가 — 확실한 반격 창이다.
        float stunEnd = Time.time + recovery;
        while (Time.time < stunEnd && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
    }

    private void ReleaseDive()
    {
        if (activeDiver == this) activeDiver = null;
    }

    protected override void Start()
    {
        base.Start();
        Health.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (Health != null) Health.OnDamaged -= HandleDamaged;
        // 잠수 도중에 죽어 코루틴째 사라져도 자리는 반드시 비워 준다.
        // 안 그러면 남은 마리들이 영영 파고들지 못한다.
        ReleaseDive();
    }

    /// <summary>땅 위에서 맞으면 도망친다. 땅속(시전 중)은 어차피 맞을 수 없다.</summary>
    private void HandleDamaged()
    {
        if (Health.IsDead || IsCasting || ExternallyBusy) return;
        if (Time.time < nextFleeTime) return;
        StartCoroutine(Flee());
    }

    /// <summary>
    /// 땅속으로 사라져 플레이어에게서 멀어진 뒤 다시 솟는다. 공격 잠수와 달리
    /// 방향이 반대고, 솟을 때 예고도 피해도 없다 — 순수한 도피다.
    /// </summary>
    private IEnumerator Flee()
    {
        ExternallyBusy = true;

        // 한 프레임 기다린다. OnDamaged는 넉백이 걸리기 전에 불리므로,
        // 바로 재면 "아직 안 밀렸다"로 보고 넉백을 잘라먹는다.
        yield return null;
        // 밀려나는 것부터 끝낸다. 도망이 넉백을 잘라먹으면 때린 값이 사라진다.
        while (Controller.IsKnockedBack && !Health.IsDead) yield return null;
        if (Health.IsDead) { ExternallyBusy = false; yield break; }

        PlayAction("Walk", -DirectionToPlayer);
        SetSubmerged(true);

        // 콜라이더가 꺼져 있어 벽이 막아 주지 않는다. 매 프레임 위치를 가두는 방식은
        // 프레임 사이에 물리가 여러 스텝을 돌아 새어 나갔다 — 대신 목표점을 먼저 범위 안으로
        // 가두고 그 점으로만 이동한다. 직선 경로는 범위 밖을 지나지 않는다.
        Vector2 center = transform.parent != null ? (Vector2)transform.parent.position : Vector2.zero;
        Vector2 target = Body.position + (-DirectionToPlayer) * fleeDistance;
        target.x = Mathf.Clamp(target.x, center.x - fleeBounds.x, center.x + fleeBounds.x);
        target.y = Mathf.Clamp(target.y, center.y - fleeBounds.y, center.y + fleeBounds.y);

        float deadline = Time.time + fleeDistance / Mathf.Max(0.5f, diveSpeed) + 0.5f;
        while (Time.time < deadline && !Health.IsDead)
        {
            Vector2 toTarget = target - Body.position;
            // 한 프레임 이동량(~0.3)보다 넉넉히 커야 목표 주변에서 앞뒤로 떨지 않는다.
            if (toTarget.magnitude <= 0.45f) break;
            Vector2 direction = toTarget.normalized;
            Body.linearVelocity = direction * diveSpeed;
            PlayAction("Walk", direction);
            yield return null;
        }

        Body.linearVelocity = Vector2.zero;
        SetSubmerged(false);
        PlayAction("Idle", DirectionToPlayer);
        yield return new WaitForSeconds(fleeRecovery);

        StopAction();
        nextFleeTime = Time.time + fleeCooldown;
        ExternallyBusy = false;
    }

    /// <summary>
    /// 땅속 상태. 반투명해지고 콜라이더가 꺼지며(부딪히지도, 맞지도 않는다 — 다른 적도
    /// 뚫고 지나간다) 그리기 순서가 캐릭터 아래로 내려간다.
    /// </summary>
    private void SetSubmerged(bool submerged)
    {
        if (spriteRenderer != null)
        {
            // 잠수 중에는 모래빛으로 물들인 반투명 몸이 곧 "모래 흔적"이다 —
            // 지금 어디를 파고 있는지는 이걸로 계속 읽을 수 있다.
            spriteRenderer.color = submerged
                ? new Color(1f, 0.87f, 0.66f, submergedAlpha)
                : Color.white;
            spriteRenderer.sortingOrder = submerged ? SubmergedSortingOrder : surfaceSortingOrder;
        }
        foreach (Collider2D col in GetComponents<Collider2D>())
            col.enabled = !submerged;
    }
}
