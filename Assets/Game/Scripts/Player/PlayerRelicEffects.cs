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
    private Health health;
    private PlayerController controller;

    /// <summary>조개껍질방울용 누적 피해량. 기준치를 넘을 때마다 회복하고 그만큼 뺀다.</summary>
    private int damageAccumulated;

    private void Awake()
    {
        health = GetComponent<Health>();
        controller = GetComponent<PlayerController>();
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
