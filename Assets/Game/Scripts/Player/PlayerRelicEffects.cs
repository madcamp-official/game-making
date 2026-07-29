using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유물이 만들어 낸 배율을 플레이어 컴포넌트들에 반영한다.
///
/// 유물 효과가 여기저기 흩어지지 않도록, "유물 → 플레이어 능력치"를 이 컴포넌트 하나가 맡는다.
/// <see cref="RelicManager"/>는 배율을 계산만 하고, 그 값을 실제로 쓰는 쪽은 전부 여기를 거친다.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerController))]
public class PlayerRelicEffects : MonoBehaviour
{
    private static readonly Collider2D[] hitBuffer = new Collider2D[32];
    private static readonly ContactFilter2D noFilter = ContactFilter2D.noFilter;
    private static readonly List<Health> struck = new List<Health>(16);

    private Health health;
    private PlayerController controller;

    /// <summary>조개껍질방울용 누적 피해량. 기준치를 넘을 때마다 회복하고 그만큼 뺀다.</summary>
    private int damageAccumulated;

    /// <summary>울퉁불퉁멧이 다시 터질 수 있는 시각. 연타로 맞을 때 반사가 도배되지 않게 한다.</summary>
    private float nextHelmetTime;

    private void Awake()
    {
        health = GetComponent<Health>();
        controller = GetComponent<PlayerController>();
        health.OnCombatDamaged += HandleCombatDamaged;
    }

    // OnEnable이 아니라 Start에서 붙는다. 오브젝트 초기화 순서는 정해져 있지 않아서
    // OnEnable 시점에는 RelicManager.Instance가 아직 없을 수 있고, 그러면 조용히 구독을 놓친다.
    // Start는 씬의 모든 Awake가 끝난 뒤에 호출되므로 Instance가 반드시 준비돼 있다.
    private void Start()
    {
        if (RelicManager.Instance != null)
            RelicManager.Instance.OnRelicsChanged += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        if (RelicManager.Instance != null)
            RelicManager.Instance.OnRelicsChanged -= Apply;
        if (health != null) health.OnCombatDamaged -= HandleCombatDamaged;
    }

    private void Apply()
    {
        RelicManager relics = RelicManager.Instance;
        if (relics == null) return;

        // 최대 체력 배율을 회복보다 먼저 반영해야 기력의 덩어리가 올바른 최대치를 기준으로 회복한다.
        health.MaxHealthMultiplier = relics.MaxHealthMultiplier;
        health.HealMultiplier = relics.HealMultiplier;
        controller.RelicSpeedMultiplier = relics.MoveSpeedMultiplier * EventBuffs.MoveSpeed;
    }

    /// <summary>
    /// 이동 속도만 다시 반영한다. 이벤트 강화는 유물 목록을 건드리지 않아
    /// <see cref="Apply"/>를 부르는 신호가 오지 않으므로, 그쪽에서 직접 호출한다.
    /// </summary>
    public void RefreshSpeed() => Apply();

    /// <summary>
    /// 플레이어가 적에게 준 피해를 알린다 (조개껍질방울). 근접 공격과 투사체 양쪽에서 호출한다.
    /// </summary>
    public static void ReportDamageDealt(int amount)
    {
        if (amount <= 0) return;

        RelicManager relics = RelicManager.Instance;
        if (relics == null || !relics.Has(RelicEffect.ShellBell)) return;

        PlayerRelicEffects effects = FindAnyObjectByType<PlayerRelicEffects>();
        if (effects != null) effects.AccumulateDamage(amount, relics);
    }

    /// <summary>
    /// 울퉁불퉁멧: 얻어맞으면 주변 적에게 그대로 되돌려 준다.
    ///
    /// 적의 피격 무적을 쓰지 않는다(<see cref="Health.TakeToll"/>). 반사는 내가 맞은 순간에
    /// 일어나는데, 그 순간은 대개 적이 방금 나를 때린 직후라 적 쪽 무적이 살아 있을 때가 많다.
    /// 무적에 걸리면 반사가 통째로 사라져 "맞으면 되돌려 준다"는 규칙이 무너진다.
    /// </summary>
    private void HandleCombatDamaged()
    {
        RelicManager relics = RelicManager.Instance;
        if (relics == null || !relics.Has(RelicEffect.RockyHelmet)) return;
        if (relics.RockyHelmetDamage <= 0 || Time.time < nextHelmetTime) return;
        nextHelmetTime = Time.time + relics.RockyHelmetCooldown;

        int damage = relics.RockyHelmetDamage;
        struck.Clear();
        int count = Physics2D.OverlapCircle(transform.position, relics.RockyHelmetRadius,
                                            noFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = hitBuffer[i].GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (struck.Contains(enemyHealth)) continue;
            struck.Add(enemyHealth);

            enemyHealth.TakeToll(damage);
            // 조개껍질방울도 반사 피해를 센다. 여기서는 자기 자신이므로 정적 진입점을 거치지 않는다.
            if (relics.Has(RelicEffect.ShellBell)) AccumulateDamage(damage, relics);
        }
    }

    private void AccumulateDamage(int amount, RelicManager relics)
    {
        damageAccumulated += amount;

        int threshold = relics.ShellBellDamagePerHeal;
        // 한 번에 기준치를 여러 번 넘길 수 있다 (강한 공격 한 방).
        while (damageAccumulated >= threshold)
        {
            damageAccumulated -= threshold;
            health.Heal(relics.ShellBellHealAmount);
        }
    }
}
