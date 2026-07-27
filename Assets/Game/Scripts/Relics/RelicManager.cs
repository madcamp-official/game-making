using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유물 등장 순서(뽑기 더미)와 보유 목록을 관리한다. 방이 바뀌어도 유지되며
/// 씬을 새로 로드(새 게임)하면 초기화된다.
///
/// 슬레이 더 스파이어 방식이다. 한 판이 시작될 때 등장 순서가 정해지고, 상점·이벤트·보스 보상은
/// 모두 이 하나의 더미에서 앞에서부터 꺼내 쓴다. 한 번 꺼낸 유물은 사지 않았더라도 더미로
/// 돌아가지 않으므로, 같은 유물이 두 번 나오는 일이 없다.
/// </summary>
public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    [Tooltip("유물 등장 순서. shuffleOrder를 끄면 이 순서 그대로 나온다.")]
    [SerializeField] private RelicData[] relicPool;

    [Tooltip("판마다 등장 순서를 섞는다. 끄면 위 배열 순서가 곧 등장 순서다.")]
    [SerializeField] private bool shuffleOrder = true;

    [Header("효과 수치")]
    [SerializeField, Min(0f)] private float amuletCoinGoldBonus = 0.25f;
    [SerializeField, Min(0f)] private float choiceBonus = 0.5f;      // 구애머리띠·안경의 증감폭
    [SerializeField, Min(0f)] private float choiceScarfSpeedBonus = 0.5f;
    [SerializeField, Min(0f)] private float choiceScarfDamagePenalty = 0.2f;
    [SerializeField, Min(0f)] private float bigRootHealBonus = 0.5f;
    [SerializeField, Min(0f)] private float wideLensScaleBonus = 0.15f;
    [SerializeField, Min(0f)] private float lifeOrbDamageBonus = 0.3f;
    [SerializeField, Range(0f, 0.9f)] private float lifeOrbMaxHealthPenalty = 0.3f;
    [SerializeField, Range(0f, 1f)] private float energyRootHealRatio = 0.33f;
    [SerializeField, Min(0)] private int leftoversHealPerRoom = 8;
    [SerializeField, Min(1)] private int shellBellDamagePerHeal = 40;
    [SerializeField, Min(0)] private int shellBellHealAmount = 3;

    private readonly List<RelicData> relics = new List<RelicData>();
    private readonly List<RelicData> upcoming = new List<RelicData>();

    public IReadOnlyList<RelicData> Relics => relics;

    /// <summary>아직 등장하지 않은 유물을 등장할 순서대로.</summary>
    public IReadOnlyList<RelicData> Upcoming => upcoming;

    /// <summary>아직 등장하지 않은 유물 수. 0이면 더 이상 유물이 나오지 않는다.</summary>
    public int RemainingCount => upcoming.Count;

    public event Action OnRelicsChanged;

    // --- 유물이 만들어 내는 배율. 유물 목록이 바뀔 때만 다시 계산한다. ---
    public float GoldMultiplier { get; private set; } = 1f;
    public float MeleeDamageMultiplier { get; private set; } = 1f;
    public float RangedDamageMultiplier { get; private set; } = 1f;
    public float MoveSpeedMultiplier { get; private set; } = 1f;
    public float HealMultiplier { get; private set; } = 1f;
    public float MaxHealthMultiplier { get; private set; } = 1f;
    public float ProjectileScale { get; private set; } = 1f;

    public int LeftoversHealPerRoom => leftoversHealPerRoom;
    public int ShellBellDamagePerHeal => shellBellDamagePerHeal;
    public int ShellBellHealAmount => shellBellHealAmount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUpcoming();
    }

    private void BuildUpcoming()
    {
        upcoming.Clear();
        if (relicPool == null) return;

        foreach (RelicData relic in relicPool)
            if (relic != null && !upcoming.Contains(relic)) upcoming.Add(relic);

        if (!shuffleOrder) return;

        // 피셔-예이츠. 한 판이 시작될 때 딱 한 번만 섞는다.
        for (int i = upcoming.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (upcoming[i], upcoming[j]) = (upcoming[j], upcoming[i]);
        }
    }

    public bool Has(RelicEffect effect) => relics.Exists(r => r.effect == effect);

    public bool Has(RelicData relic) => relic != null && relics.Contains(relic);

    /// <summary>
    /// 등장 순서에서 다음 유물을 꺼낸다. 꺼낸 유물은 사든 사지 않든 더미로 돌아가지 않는다.
    /// 더 이상 남은 유물이 없으면 null.
    /// </summary>
    public RelicData DrawNext()
    {
        if (upcoming.Count == 0) return null;
        RelicData next = upcoming[0];
        upcoming.RemoveAt(0);
        return next;
    }

    /// <summary>등장 순서에서 <paramref name="count"/>개까지 꺼낸다 (상점처럼 여러 칸이 필요할 때).</summary>
    public List<RelicData> DrawNext(int count)
    {
        List<RelicData> drawn = new List<RelicData>(Mathf.Max(0, count));
        for (int i = 0; i < count; i++)
        {
            RelicData next = DrawNext();
            if (next == null) break;
            drawn.Add(next);
        }
        return drawn;
    }

    /// <summary>
    /// 보상으로 유물 하나를 지급한다. <paramref name="fixedRelic"/>이 지정돼 있고 아직 없는
    /// 유물이면 그것을, 아니면 등장 순서에서 다음 유물을 준다. 줄 유물이 없으면 아무 일도 없다.
    /// </summary>
    public static void GrantReward(RelicData fixedRelic)
    {
        RelicManager manager = Instance;
        if (manager == null) return;

        RelicData relic = fixedRelic != null && !manager.Has(fixedRelic)
            ? fixedRelic
            : manager.DrawNext();
        if (relic != null) manager.AddRelic(relic);
    }

    /// <summary>
    /// 유물을 지급한다. 이미 가진 유물이거나, 구애 시리즈를 이미 하나 지니고 있으면 아무 일도 하지 않는다.
    /// </summary>
    public void AddRelic(RelicData relic)
    {
        if (relic == null || relics.Contains(relic)) return;

        // 구애 시리즈는 동시에 하나만 지닐 수 있다. 보통은 더미에서 미리 빠지지만,
        // 고정 보상처럼 더미를 거치지 않는 경로도 있으므로 여기서도 막는다.
        if (relic.IsChoiceItem && relics.Exists(r => r.IsChoiceItem)) return;

        relics.Add(relic);
        // 상점 진열 등으로 아직 더미에 남아 있었다면 같이 빼 준다.
        upcoming.Remove(relic);

        // 구애 시리즈는 한 번에 하나만 지닐 수 있으므로 나머지는 더 이상 등장시키지 않는다.
        if (relic.IsChoiceItem)
            upcoming.RemoveAll(r => r.IsChoiceItem);

        RecalculateModifiers();
        OnRelicsChanged?.Invoke();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowRelicAcquired(relic, 3f);

        ApplyOnAcquire(relic);
    }

    private void RecalculateModifiers()
    {
        GoldMultiplier = 1f;
        MeleeDamageMultiplier = 1f;
        RangedDamageMultiplier = 1f;
        MoveSpeedMultiplier = 1f;
        HealMultiplier = 1f;
        MaxHealthMultiplier = 1f;
        ProjectileScale = 1f;

        foreach (RelicData relic in relics)
        {
            switch (relic.effect)
            {
                case RelicEffect.AmuletCoin:
                    GoldMultiplier *= 1f + amuletCoinGoldBonus;
                    break;
                case RelicEffect.ChoiceBand:
                    MeleeDamageMultiplier *= 1f + choiceBonus;
                    RangedDamageMultiplier *= 1f - choiceBonus;
                    break;
                case RelicEffect.ChoiceSpecs:
                    RangedDamageMultiplier *= 1f + choiceBonus;
                    MeleeDamageMultiplier *= 1f - choiceBonus;
                    break;
                case RelicEffect.ChoiceScarf:
                    MoveSpeedMultiplier *= 1f + choiceScarfSpeedBonus;
                    MeleeDamageMultiplier *= 1f - choiceScarfDamagePenalty;
                    RangedDamageMultiplier *= 1f - choiceScarfDamagePenalty;
                    break;
                case RelicEffect.BigRoot:
                    HealMultiplier *= 1f + bigRootHealBonus;
                    break;
                case RelicEffect.WideLens:
                    ProjectileScale *= 1f + wideLensScaleBonus;
                    break;
                case RelicEffect.LifeOrb:
                    MeleeDamageMultiplier *= 1f + lifeOrbDamageBonus;
                    RangedDamageMultiplier *= 1f + lifeOrbDamageBonus;
                    MaxHealthMultiplier *= 1f - lifeOrbMaxHealthPenalty;
                    break;
            }
        }
    }

    private void ApplyOnAcquire(RelicData relic)
    {
        switch (relic.effect)
        {
            // 행복의알: 획득 즉시(=보스방 진입 전에) 다음 단계로 진화한다.
            case RelicEffect.HappyEgg:
                PlayerEvolution evolution = FindAnyObjectByType<PlayerEvolution>();
                if (evolution != null) evolution.Evolve();
                break;

            // 기력의 덩어리: 획득 즉시 최대 체력의 일정 비율을 회복한다.
            // 생명의구슬로 줄어든 최대 체력이 이미 반영된 값을 기준으로 삼는다.
            case RelicEffect.EnergyRoot:
                PlayerController player = FindAnyObjectByType<PlayerController>();
                Health health = player != null ? player.GetComponent<Health>() : null;
                if (health != null)
                    health.Heal(GameMath.RoundHalfUp(health.MaxHealth * energyRootHealRatio));
                break;
        }
    }
}
