using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 기술이 바닥에 까는 원형 장판. 씨뿌리기(회복)와 꽃잎댄스(피해)가 같이 쓴다.
///
/// 보스가 쓰는 <see cref="DamageZone"/>과 나눠 둔 이유: 그쪽은 "플레이어를 때린다"가 전부라
/// 대상이 반대인 이 둘을 끼워 넣으면 조건문만 늘어난다. 대신 판정 방식은 같게 맞췄다 —
/// 물리 트리거가 아니라 중심 거리로 재서, 그린 원이 곧 효과 범위가 된다.
///
/// 자리를 잡는 방식은 둘로 갈린다.
/// * 씨뿌리기 — 시전한 자리에 고정. 따라다니면 "장판 위에 서 있기"라는 선택 자체가 없어진다.
/// * 꽃잎댄스 — 플레이어를 따라다닌다. 적을 장판 위로 끌어들이는 게 아니라, 적에게 붙어
///   비비는 근접 기술이라 몸에 붙어 있어야 쓸모가 있다.
/// </summary>
public class MoveZone : MonoBehaviour
{
    /// <summary>보스 장판과 같은 높이. 지형보다 위, 캐릭터보다 아래.</summary>
    public const int SortingOrder = 1;

    private enum Mode { HealPlayer, DamageEnemies }

    private static readonly Collider2D[] hitBuffer = new Collider2D[32];
    private static readonly ContactFilter2D noFilter = ContactFilter2D.noFilter;
    private static readonly List<Health> struck = new List<Health>(16);

    private Mode mode;
    private float radius;
    private int amount;
    private float tickInterval;
    private float expireTime;
    private float nextTickTime;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer edgeRenderer;
    private float baseAlpha;
    private float edgeBaseAlpha;
    private Transform player;
    private Health playerHealth;
    /// <summary>비어 있지 않으면 매 프레임 이 대상의 위치로 따라간다.</summary>
    private Transform follow;

    /// <summary>씨뿌리기: 안에 선 플레이어를 주기적으로 회복시킨다. 깔린 자리에 고정된다.</summary>
    public static MoveZone SpawnHeal(Vector2 center, float radius, float duration,
                                     int healPerTick, float tickInterval, Color color) =>
        Spawn(Mode.HealPlayer, "SeedZone", center, radius, duration, healPerTick, tickInterval, color, null);

    /// <summary>
    /// 꽃잎댄스: 안에 있는 적을 주기적으로 때린다.
    /// <paramref name="follow"/>를 주면 그 대상을 중심으로 따라다닌다.
    /// </summary>
    public static MoveZone SpawnDamage(Vector2 center, float radius, float duration,
                                       int damagePerTick, float tickInterval, Color color,
                                       Transform follow = null) =>
        Spawn(Mode.DamageEnemies, "PetalZone", center, radius, duration, damagePerTick, tickInterval,
              color, follow);

    private static MoveZone Spawn(Mode mode, string name, Vector2 center, float radius, float duration,
                                  int amount, float tickInterval, Color color, Transform follow)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center;
        go.transform.localScale = Vector3.one * (radius * 2f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Circle;
        sr.color = color;
        sr.sortingOrder = SortingOrder;

        // 테두리를 두른다. 1층 바닥이 초록이라 초록 장판은 채움만으로는 묻힌다 —
        // 버터플 은빛바람과 코뿌리 뿔 예고에서 이미 한 번씩 겪은 문제다.
        GameObject edgeGo = new GameObject("Edge");
        edgeGo.transform.SetParent(go.transform, false);
        SpriteRenderer edge = edgeGo.AddComponent<SpriteRenderer>();
        edge.sprite = PrimitiveSprites.Ring;
        edge.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 2.4f));
        edge.sortingOrder = SortingOrder;

        MoveZone zone = go.AddComponent<MoveZone>();
        zone.mode = mode;
        zone.spriteRenderer = sr;
        zone.edgeRenderer = edge;
        zone.baseAlpha = color.a;
        zone.edgeBaseAlpha = edge.color.a;
        zone.radius = radius;
        zone.amount = amount;
        zone.tickInterval = tickInterval;
        zone.follow = follow;
        zone.expireTime = Time.time + duration;
        // 깔자마자 한 번 효과가 들어가야 짧은 장판이 헛돌지 않는다.
        zone.nextTickTime = Time.time;
        return zone;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        player = pc.transform;
        playerHealth = pc.GetComponent<Health>();
    }

    private void Update()
    {
        if (Time.time >= expireTime) { Destroy(gameObject); return; }

        // 따라다니는 장판은 판정보다 먼저 자리를 옮긴다. 순서가 반대면 한 틱 늦은 자리에서 때린다.
        if (follow != null) transform.position = follow.position;

        // 사라지기 직전 0.6초 동안 옅어져서 곧 없어진다는 걸 알린다.
        float remaining = expireTime - Time.time;
        if (remaining < 0.6f)
        {
            float t = remaining / 0.6f;
            SetAlpha(spriteRenderer, baseAlpha * t);
            SetAlpha(edgeRenderer, edgeBaseAlpha * t);
        }

        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + tickInterval;

        if (mode == Mode.HealPlayer) HealPlayer();
        else DamageEnemies();
    }

    private static void SetAlpha(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    private void HealPlayer()
    {
        if (player == null || playerHealth == null || playerHealth.IsDead) return;
        if (Vector2.Distance(player.position, transform.position) > radius) return;
        playerHealth.Heal(amount);
    }

    private void DamageEnemies()
    {
        struck.Clear();
        int count = Physics2D.OverlapCircle(transform.position, radius, noFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = hitBuffer[i].GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            // 적 하나가 콜라이더를 여럿 가져도 한 틱에 한 번만 맞는다.
            if (struck.Contains(enemyHealth)) continue;
            struck.Add(enemyHealth);

            // 무적 시간을 쓰지 않는다. 0.5초마다 도는 장판이 적의 피격 무적에 걸리면
            // 틱이 통째로 사라져, 서 있는 시간만큼 아프다는 규칙이 무너진다.
            enemyHealth.TakeToll(amount);
            PlayerRelicEffects.ReportDamageDealt(amount);
        }
    }
}
