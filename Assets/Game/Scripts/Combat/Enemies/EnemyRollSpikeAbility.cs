using System.Collections;
using UnityEngine;

/// <summary>
/// 고지의 공격. 몸을 말아 <b>짧게 굴러 붙은 뒤 사방으로 가시를 뿌린다.</b>
///
/// 네 단계다.
///
/// <list type="number">
/// <item><b>예고</b> — 발톱을 들어(StrikeReady) 굴러갈 선을 그린다. 방향은 여기서 고정된다.</item>
/// <item><b>돌진</b> — 몸을 말며(Attack) 그 선을 따라 짧게 구른다. 벽에 부딪히면 그 자리에서 멈춘다.</item>
/// <item><b>웅크림</b> — 멈춘 자리에서 공이 된 채(Guard) 가시가 뻗을 길을 전부 그려 준다.</item>
/// <item><b>발사</b> — 몸을 펴며(Uncurl) 모든 방향으로 동시에 가시를 쏜다.</item>
/// </list>
///
/// 동작 넷이 한 줄로 이어지도록 시간을 맞췄다. Attack 시트는 0.37초 동안 <b>몸 말기(0~2) →
/// 구르기(3~9) → 몸 펴기(10)</b>를 지나는데, 구르는 시간(거리÷속도 = 0.28초)이 프레임 9쯤에서
/// 끝나므로 마지막 "펴진" 프레임에 닿지 않고 공 모양인 채로 Guard에 이어진다.
/// <b>구르는 시간을 늘리려면 Attack의 길이도 함께 봐야 한다</b> — 0.35초를 넘기면 굴러가는
/// 도중에 몸이 펴진 그림이 한 번 스친다.
///
/// 예전에는 붙어서 한 번 크게 할퀸 뒤 공처럼 말려 버티는 방어형이었다. 그때는 "굳은 고지를
/// 계속 때릴 것인가"가 물음이었는데, 지금은 <b>어디에 설 것인가</b>가 물음이다 — 돌진은
/// 선을 보고 옆으로 비키면 되고, 가시는 갈래 사이의 빈틈에 서면 된다. 둘 다 자리로 푼다.
///
/// 돌진과 가시를 잇는 이유: 굴러온 자리가 곧 가시의 중심이다. 돌진을 피해 비켜선 자리가
/// 하필 가시 갈래 위일 수 있어서, 첫 회피가 두 번째 회피를 정한다. 한 패턴 안에서
/// 두 번 읽게 만드는 것이 이 적의 값이다.
///
/// 가시는 <see cref="EnemyProjectile"/>이라 <b>다른 적을 통과하고</b> 벽이나 플레이어에게
/// 닿으면 사라진다 — 고지가 여럿이어도 서로의 가시를 막지 않는다.
/// </summary>
public class EnemyRollSpikeAbility : EnemyAbility
{
    [Header("동작 이름")]
    [Tooltip("굴러갈 방향을 예고하는 동안의 자세. 아직 몸을 말기 전이다.")]
    [SerializeField] private string readyState = "StrikeReady";
    [Tooltip("몸을 말며 굴러가는 동작. 고지는 Attack 시트가 곧 말기＋구르기다.")]
    [SerializeField] private string rollState = "Attack";
    [Tooltip("공 모양 정지 자세. 가시를 준비하는 동안 웅크린 채로 있는다.")]
    [SerializeField] private string curlState = "Guard";
    [Tooltip("몸을 펴는 동작. 가시를 쏘는 순간에 재생한다.")]
    [SerializeField] private string uncurlState = "Uncurl";

    [Header("돌진")]
    [Tooltip("굴러갈 선을 보여 주는 시간. 이 시간이 끝나면 방향이 바뀌지 않는다.")]
    [SerializeField, Min(0f)] private float windup = 0.5f;
    [Tooltip("굴러가는 거리. 스라크(5.5)의 절반쯤이다 — 붙는 수단이지 도망칠 거리가 아니다. " +
             "벽에 막히면 더 짧아진다.")]
    [SerializeField, Min(0f)] private float dashDistance = 2.8f;
    [SerializeField, Min(0f)] private float dashSpeed = 10f;
    [SerializeField, Min(0)] private int dashDamage = 12;
    [Tooltip("구르는 몸의 판정 반지름.")]
    [SerializeField, Min(0f)] private float dashHitRadius = 0.6f;

    [Header("가시")]
    [Tooltip("웅크린 채 가시 길을 보여 주는 시간.")]
    [SerializeField, Min(0f)] private float spikeWindup = 0.55f;
    [Tooltip("사방으로 뻗는 가시의 개수. 갈래 사이의 빈틈이 곧 설 자리다 — " +
             "늘릴수록 빈틈이 좁아진다.")]
    [SerializeField, Min(2)] private int spikeCount = 6;
    [SerializeField, Min(0f)] private float spikeSpeed = 8f;
    [SerializeField, Min(0)] private int spikeDamage = 12;
    [Tooltip("가시의 판정 반지름. 그림은 이보다 길쭉하지만 판정은 이 원이다.")]
    [SerializeField, Min(0f)] private float spikeRadius = 0.17f;
    [Tooltip("가시가 날아가는 시간. 벽에 닿으면 그 전에 사라진다.")]
    [SerializeField, Min(0.1f)] private float spikeLifetime = 1.6f;
    [Tooltip("예고선의 길이. 실제 가시는 벽까지 날아가지만, 위험한 구간은 이 안이다.")]
    [SerializeField, Min(1f)] private float spikeTelegraphLength = 5f;

    [Header("마무리")]
    [Tooltip("몸을 펴는 동작(Uncurl, 프레임 4개)의 길이.")]
    [SerializeField, Min(0f)] private float uncurlDuration = 0.14f;
    [Tooltip("몸을 편 뒤 무방비로 정지하는 시간. 이때가 반격의 창이다.")]
    [SerializeField, Min(0f)] private float recovery = 0.9f;

    [Header("색")]
    // 2층 바닥이 밝은 모래(연노랑)라 주황 계열은 묻힌다. 다른 2층 적의 피해 범위와 같은
    // 붉은색을 쓴다 — 색이 곧 "여기 서 있으면 맞는다"라는 뜻이어야 한눈에 읽힌다.
    [SerializeField] private Color warningColor = new Color(0.88f, 0.12f, 0.2f, 0.42f);
    [SerializeField] private Color spikeColor = new Color(0.95f, 0.92f, 0.78f, 1f);

    /// <summary>돌진 예고선의 굵기. 몸통이 지나갈 폭이다.</summary>
    private const float DashTelegraphWidth = 0.9f;

    /// <summary>가시가 몸에서 떨어져 나오는 거리. 발밑에서 튀어나오면 무엇이 나갔는지 안 보인다.</summary>
    private const float SpikeSpawnOffset = 0.4f;

    /// <summary>가시 그림을 나아가는 방향으로 늘리는 배율. 판정은 늘어나지 않는다.</summary>
    private const float SpikeStretch = 3.2f;

    /// <summary>
    /// 막혔는지 재는 창. 속도는 Update에서 넣고 실제 이동은 FixedUpdate에서 일어나므로,
    /// 프레임 단위로 재면 멀쩡히 굴러가는 중에도 "안 움직였다"가 자주 나온다.
    /// </summary>
    private const float StallWindow = 0.15f;

    private EnemyProjectilePool pool;

    // 구르는 동안에만 플레이어와의 충돌을 끈다. 몸이 플레이어에게 막히면 밀기만 하다
    // 판정 거리 밖에서 멈춰, 정면으로 굴러도 절대 맞지 않는다 (스라크 돌진과 같은 이유).
    private readonly System.Collections.Generic.List<Collider2D> ownColliders =
        new System.Collections.Generic.List<Collider2D>();
    private readonly System.Collections.Generic.List<Collider2D> playerColliders =
        new System.Collections.Generic.List<Collider2D>();

    protected override void Awake()
    {
        base.Awake();
        CollectSolid(GetComponentsInChildren<Collider2D>(true), ownColliders);
    }

    protected override void Start()
    {
        base.Start();
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) CollectSolid(pc.GetComponentsInChildren<Collider2D>(true), playerColliders);

        // 풀을 적이 아니라 방에 붙인다. 적과 함께 사라지면 쏘고 죽었을 때 날아가던 가시가
        // 공중에서 없어진다. 적의 배율(1.25)이 콜라이더 반지름에 섞이는 것도 막는다.
        pool = EnemyProjectilePool.Create(EffectRoot, spikeCount * 2);
        pool.SetArena(RoomArena.CenterOf(transform), RoomArena.HalfSize);
    }

    private static void CollectSolid(Collider2D[] source,
                                     System.Collections.Generic.List<Collider2D> into)
    {
        into.Clear();
        foreach (Collider2D collider in source)
            if (collider != null && !collider.isTrigger) into.Add(collider);
    }

    private void SetPassThroughPlayer(bool ignore)
    {
        foreach (Collider2D mine in ownColliders)
        {
            if (mine == null) continue;
            foreach (Collider2D theirs in playerColliders)
                if (theirs != null) Physics2D.IgnoreCollision(mine, theirs, ignore);
        }
    }

    protected override IEnumerator Perform()
    {
        // 1. 예고 — 굴러갈 선을 그린다. 방향은 여기서 정해져 끝까지 바뀌지 않는다.
        //    선을 보고 옆으로 비키는 것이 정답이 되려면, 구르는 도중에 다시 쫓으면 안 된다.
        Vector2 aim = DirectionToPlayer;
        AttackTelegraph path = AttackTelegraph.CreateLine(
            EffectRoot, transform.position, aim, dashDistance, DashTelegraphWidth, warningColor);
        path.Pulse(windup);
        PlayAction(readyState, aim);

        float windupEnd = Time.time + windup;
        while (Time.time < windupEnd && !Health.IsDead)
        {
            HoldPosition();
            // 그린 자리를 몸에 붙여 둔다. 예고 중에 넉백으로 밀려날 수 있는데
            // (HoldPosition이 그 0.15초는 놓아 준다), 그림만 처음 자리에 남으면 어긋난다.
            if (path != null)
                path.transform.position = (Vector2)transform.position + aim * (dashDistance * 0.5f);
            yield return null;
        }
        if (Health.IsDead) yield break;

        // 2. 돌진 — 몸을 말며 예고한 방향으로 짧게 구른다.
        //    반드시 처음부터 다시 재생한다(Replay). 말기 프레임부터 보여야 "굴러간다"가 된다.
        ReplayAction(rollState, aim);
        yield return Roll(aim);
        if (Health.IsDead) yield break;

        // 3. 웅크림 — 멈춘 자리에서 공인 채로, 가시가 뻗을 길을 전부 그려 준다.
        //    구르기가 공 모양 프레임에서 끝나므로 Guard로 넘어가도 그림이 튀지 않는다.
        //    시작 각도를 매번 새로 뽑는다. 고정하면 방향이 외워져 늘 같은 자리에 서면 된다.
        float spin = Random.Range(0f, 360f / spikeCount);
        PlayAction(curlState, aim);

        var rays = new AttackTelegraph[spikeCount];
        for (int i = 0; i < spikeCount; i++)
        {
            Vector2 direction = SpikeDirection(i, spin);
            rays[i] = AttackTelegraph.CreateLine(EffectRoot, transform.position, direction,
                spikeTelegraphLength, spikeRadius * 2f, warningColor);
            rays[i].Pulse(spikeWindup);
        }

        float spikeEnd = Time.time + spikeWindup;
        while (Time.time < spikeEnd && !Health.IsDead)
        {
            HoldPosition();
            // 가시는 몸에서 나가므로 예고도 몸을 따라간다.
            Vector2 at = transform.position;
            for (int i = 0; i < spikeCount; i++)
                if (rays[i] != null)
                    rays[i].transform.position = at + SpikeDirection(i, spin) * (spikeTelegraphLength * 0.5f);
            yield return null;
        }
        if (Health.IsDead) yield break;

        // 4. 발사 — 몸을 펴며 모든 방향으로 동시에 쏜다.
        ReplayAction(uncurlState, aim);
        Vector2 muzzle = transform.position;
        for (int i = 0; i < spikeCount; i++)
        {
            EnemyProjectile spike = pool != null ? pool.Borrow() : null;
            if (spike == null) continue;

            Vector2 direction = SpikeDirection(i, spin);
            spike.Launch(muzzle + direction * SpikeSpawnOffset, direction,
                spikeSpeed, spikeDamage, spikeLifetime, spikeRadius, spikeColor,
                PrimitiveSprites.Triangle, SpikeStretch);
        }

        // 5. 몸을 다 펴고 나면 한동안 무방비로 굳는다.
        //    편 자세를 그대로 붙들고 있는다. 여기서 동작을 놓아 버리면 제자리에 선 채로
        //    걷는 그림이 돌아 — 기본 추적이 꺼져 있어 IsEngaged가 참으로 남는다 —
        //    "굳었다"가 읽히지 않는다.
        yield return new WaitForSeconds(uncurlDuration);
        float pauseEnd = Time.time + recovery;
        while (Time.time < pauseEnd && !Health.IsDead)
        {
            HoldPosition();
            yield return null;
        }
        StopAction();
    }

    /// <summary><paramref name="index"/>번째 가시가 나아갈 방향. 예고와 발사가 같은 식을 쓴다.</summary>
    private Vector2 SpikeDirection(int index, float spin)
    {
        float degrees = spin + 360f / spikeCount * index;
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    /// <summary>예고한 방향으로 굴러간다. 벽에 막히면 그 자리에서 멈춘다.</summary>
    private IEnumerator Roll(Vector2 direction)
    {
        SetPassThroughPlayer(true);

        float duration = dashDistance / Mathf.Max(0.01f, dashSpeed);
        float elapsed = 0f;
        bool hit = false;

        Vector2 checkpoint = transform.position;
        float sinceCheckpoint = 0f;

        while (elapsed < duration && !Health.IsDead)
        {
            Body.linearVelocity = direction * dashSpeed;
            elapsed += Time.deltaTime;
            sinceCheckpoint += Time.deltaTime;

            // 한 번 구르는 동안 한 번만 때린다. 스치기만 해도 여러 번 맞으면 즉사한다.
            if (!hit && TryHitPlayer()) hit = true;

            if (sinceCheckpoint >= StallWindow)
            {
                // 벽에 막혀 더 나아가지 못하면 남은 시간을 버린다. 가시는 여기서 뿌려진다.
                if (Vector2.Distance(checkpoint, transform.position) < dashSpeed * StallWindow * 0.25f) break;
                checkpoint = transform.position;
                sinceCheckpoint = 0f;
            }

            yield return null;
        }

        // 죽어서 빠져나온 경우까지 포함해 반드시 되돌린다.
        SetPassThroughPlayer(false);
        Body.linearVelocity = Vector2.zero;
    }

    private bool TryHitPlayer()
    {
        if (dashDamage <= 0) return false;
        if (PlayerHealth == null || PlayerHealth.IsDead || PlayerHealth.IsInvincible) return false;
        if (Vector2.Distance(transform.position, PlayerPosition) > dashHitRadius) return false;

        PlayerHealth.TakeDamage(dashDamage);
        return true;
    }
}
