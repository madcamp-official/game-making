using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유물 등장 순서(뽑기 더미)와 보유 목록을 관리한다. 방이 바뀌어도 유지되며
/// 씬을 새로 로드(새 게임)하면 초기화된다.
///
/// 슬레이 더 스파이어 방식이다. 한 판이 시작될 때 등장 순서가 정해지고, 상점·이벤트·보스 보상은
/// 모두 이 하나의 더미에서 앞에서부터 꺼내 쓴다. 한 번 꺼낸 유물은 사지 않았더라도 그 판에서
/// 다시 나오지 않는다 — 단, 더미를 끝까지 다 본 뒤에는 <see cref="Refill"/>이 아직 손에 넣지
/// 않은 유물로 더미를 새로 만든다. 그래서 얻을 수 있는 유물이 하나라도 남아 있는 한
/// 상점 자리가 비는 일은 없다.
///
/// 증감은 전부 <b>합연산</b>이다. +30%와 +20%를 함께 지니면 1.3×1.2가 아니라 +50%다.
/// 예외는 구애 시리즈뿐인데, 그쪽은 다른 증감을 모두 더한 뒤 <b>마지막에 곱해진다</b>.
/// </summary>
public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    [Tooltip("유물 등장 순서. shuffleOrder를 끄면 이 순서 그대로 나온다.")]
    [SerializeField] private RelicData[] relicPool;

    [Tooltip("판마다 등장 순서를 섞는다. 끄면 위 배열 순서가 곧 등장 순서다.")]
    [SerializeField] private bool shuffleOrder = true;

    [Header("효과 수치 — 합연산으로 쌓인다")]
    [SerializeField, Min(0f)] private float amuletCoinGoldBonus = 0.25f;
    [SerializeField, Min(0f)] private float bigRootHealBonus = 0.5f;
    [SerializeField, Min(0f)] private float wideLensSizeBonus = 0.15f;
    [SerializeField, Min(0f)] private float lifeOrbDamageBonus = 0.3f;
    [SerializeField, Range(0f, 0.9f)] private float lifeOrbMaxHealthPenalty = 0.3f;
    [SerializeField, Min(0f)] private float hpUpMaxHealthBonus = 0.15f;
    [SerializeField, Min(0f)] private float proteinMeleeBonus = 0.15f;
    [SerializeField, Min(0f)] private float calciumRangedBonus = 0.15f;
    [SerializeField, Min(0f)] private float carbosMoveSpeedBonus = 0.1f;
    [SerializeField, Range(0f, 0.9f)] private float quickClawCooldownReduction = 0.15f;
    [SerializeField, Min(0f)] private float lightClayZoneDurationBonus = 0.3f;

    [Header("효과 수치 — 구애 시리즈 (맨 마지막에 곱해진다)")]
    [SerializeField, Min(0f)] private float choiceBonus = 0.3f;               // 머리띠·안경이 올려 주는 폭
    [SerializeField, Range(0f, 1f)] private float choicePenalty = 0.5f;       // 머리띠·안경이 깎는 쪽에 곱하는 값
    [SerializeField, Min(0f)] private float choiceScarfSpeedBonus = 0.2f;
    [SerializeField, Range(0f, 1f)] private float choiceScarfDamage = 0.8f;   // 스카프가 양쪽 피해에 곱하는 값

    [Header("효과 수치 — 그 밖")]
    // 자뭉열매의 회복 비율은 여기 없다. 유물에서 빠지고 상점 포션이 됐다 (ShopController).
    [SerializeField, Min(0)] private int leftoversHealPerRoom = 12;
    [SerializeField, Min(1)] private int shellBellDamagePerHeal = 40;
    [SerializeField, Min(0)] private int shellBellHealAmount = 6;
    [SerializeField, Min(0)] private int nuggetGold = 100;
    [SerializeField, Min(0)] private int rockyHelmetDamage = 10;
    [Tooltip("울퉁불퉁멧이 반사 피해를 주는 범위. 타일 한 칸이 1이다.")]
    [SerializeField, Min(0f)] private float rockyHelmetRadius = 2.2f;
    // 울퉁불퉁멧에는 따로 쿨타임이 없다. 전투 피해를 입으면 플레이어 쪽 피격 무적(0.5초)이
    // 함께 걸리므로, 반사가 나가는 간격은 그 무적이 이미 정해 준다.
    [Tooltip("기술머신을 지녔을 때 강화 화면에 뜨는 선택지 수.")]
    [SerializeField, Min(1)] private int techMachineUpgradeOptions = 4;

    [Header("상점 가격")]
    [Tooltip("희귀도 1·2·3단계의 기준 가격. RelicRarity 순서와 같아야 한다.")]
    [SerializeField] private int[] rarityPrices = { 90, 120, 150 };
    [Tooltip("기준 가격에서 위아래로 흔들리는 폭. 5면 90짜리가 85~95로 나온다.")]
    [SerializeField, Min(0)] private int priceJitter = 5;

    private readonly List<RelicData> relics = new List<RelicData>();
    private readonly List<RelicData> upcoming = new List<RelicData>();

    private PlayerEvolution cachedEvolution;

    public IReadOnlyList<RelicData> Relics => relics;

    /// <summary>아직 등장하지 않은 유물을 등장할 순서대로.</summary>
    public IReadOnlyList<RelicData> Upcoming => upcoming;

    /// <summary>지금 더미에 남은 유물 수. 0이어도 아직 못 얻은 유물이 있으면 더미가 다시 채워진다.</summary>
    public int RemainingCount => upcoming.Count;

    public event Action OnRelicsChanged;

    // --- 유물이 만들어 내는 배율. 유물 목록이 바뀔 때만 다시 계산한다. ---
    public float GoldMultiplier { get; private set; } = 1f;
    public float MeleeDamageMultiplier { get; private set; } = 1f;
    public float RangedDamageMultiplier { get; private set; } = 1f;
    public float MoveSpeedMultiplier { get; private set; } = 1f;
    public float HealMultiplier { get; private set; } = 1f;
    public float MaxHealthMultiplier { get; private set; } = 1f;
    /// <summary>플레이어 공격의 판정 크기 배율 (광각렌즈).</summary>
    public float AttackSizeMultiplier { get; private set; } = 1f;
    /// <summary>시간으로 도는 쿨타임에 곱하는 값 (선제공격손톱). 작을수록 빨리 돌아온다.</summary>
    public float CooldownMultiplier { get; private set; } = 1f;
    /// <summary>장판 지속시간 배율 (빛의점토).</summary>
    public float ZoneDurationMultiplier { get; private set; } = 1f;

    public int LeftoversHealPerRoom => leftoversHealPerRoom;
    public int ShellBellDamagePerHeal => shellBellDamagePerHeal;
    public int ShellBellHealAmount => shellBellHealAmount;
    public int RockyHelmetDamage => rockyHelmetDamage;
    public float RockyHelmetRadius => rockyHelmetRadius;

    /// <summary>
    /// 이 유물의 상점 가격. 희귀도별 기준 가격에서 <c>priceJitter</c>만큼 위아래로 흔든다.
    ///
    /// 부를 때마다 값이 달라지므로 <b>진열할 때 한 번만 부르고 그 값을 들고 있어야 한다</b>
    /// (<see cref="ShopController"/>). 매 프레임 다시 물으면 가격표가 춤춘다.
    /// </summary>
    public int PriceOf(RelicData relic)
    {
        if (relic == null || rarityPrices == null || rarityPrices.Length == 0) return 0;
        int index = Mathf.Clamp((int)relic.rarity, 0, rarityPrices.Length - 1);
        return Mathf.Max(0, rarityPrices[index] +
                            UnityEngine.Random.Range(-priceJitter, priceJitter + 1));
    }

    /// <summary>강화 화면에 띄울 선택지 수. 기술머신이 있으면 하나 늘어난다.</summary>
    public int UpgradeOptionCount(int defaultCount) =>
        Has(RelicEffect.TechMachine) ? Mathf.Max(defaultCount, techMachineUpgradeOptions) : defaultCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Refill();
    }

    /// <summary>
    /// 새 판을 시작한다. 지닌 유물을 전부 버리고 더미를 처음부터 다시 만든다.
    /// 씬을 다시 올리지 않고 이어서 도는 구조라 여기서 직접 비워야 한다.
    /// </summary>
    public void ResetForNewRun()
    {
        relics.Clear();
        Refill();
        OnRelicsChanged?.Invoke();
    }

    /// <summary>
    /// 더미를 아직 손에 넣지 않은 유물로 새로 만든다. 판이 시작될 때 한 번,
    /// 그리고 더미를 끝까지 다 본 뒤에 다시 불린다.
    ///
    /// 한 번 지나친 유물이 다시 돌아오는 것이 핵심이다. 예전에는 더미가 마르면 상점의
    /// 유물 자리가 통째로 비었는데, 얻을 수 있는 유물이 남아 있는데도 살 수 없는 것은
    /// 골드를 모으는 이유 자체를 없앤다.
    /// </summary>
    private void Refill()
    {
        upcoming.Clear();
        if (relicPool == null) return;

        bool hasChoice = relics.Exists(r => r.IsChoiceItem);
        foreach (RelicData relic in relicPool)
        {
            if (relic == null || upcoming.Contains(relic)) continue;
            if (relics.Contains(relic)) continue;              // 이미 지니고 있다
            if (relic.IsChoiceItem && hasChoice) continue;     // 구애 시리즈는 하나뿐
            upcoming.Add(relic);
        }

        if (!shuffleOrder) return;

        // 피셔-예이츠.
        for (int i = upcoming.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (upcoming[i], upcoming[j]) = (upcoming[j], upcoming[i]);
        }
    }

    public bool Has(RelicEffect effect) => relics.Exists(r => r.effect == effect);

    public bool Has(RelicData relic) => relic != null && relics.Contains(relic);

    /// <summary>
    /// 등장 순서에서 다음 유물을 꺼낸다. 더미가 비어 있으면 아직 못 얻은 유물로 다시 채워서
    /// 꺼낸다. 얻을 수 있는 유물이 하나도 남지 않았을 때만 null이다.
    /// </summary>
    public RelicData DrawNext() => Draw(null);

    /// <summary>
    /// 조건에 맞는 다음 유물을 꺼낸다. <paramref name="skip"/>이 true를 돌려주는 유물은 건너뛴다.
    /// 더미가 다 떨어지면 한 번만 다시 채우고 재시도한다 — 두 번째도 못 찾으면 정말 없는 것이다.
    /// </summary>
    private RelicData Draw(Predicate<RelicData> skip)
    {
        RelicData found = TakeFrom(upcoming, skip);
        if (found != null) return found;

        Refill();
        return TakeFrom(upcoming, skip);
    }

    private static RelicData TakeFrom(List<RelicData> list, Predicate<RelicData> skip)
    {
        int index = skip == null ? (list.Count > 0 ? 0 : -1) : list.FindIndex(r => !skip(r));
        if (index < 0) return null;
        RelicData relic = list[index];
        list.RemoveAt(index);
        return relic;
    }

    /// <summary>
    /// 등장 순서에서 <paramref name="count"/>개까지 꺼낸다 (상점처럼 여러 칸이 필요할 때).
    /// 더미가 중간에 다시 채워져도 한 번에 꺼낸 것끼리는 겹치지 않는다.
    /// </summary>
    public List<RelicData> DrawNext(int count)
    {
        List<RelicData> drawn = new List<RelicData>(Mathf.Max(0, count));
        for (int i = 0; i < count; i++)
        {
            RelicData next = Draw(drawn.Contains);
            if (next == null) break;
            drawn.Add(next);
        }
        return drawn;
    }

    /// <summary>
    /// 등장 순서에서 구애 시리즈를 건너뛰고 다음 유물을 꺼낸다.
    /// 이벤트 보상처럼 "무엇이 나올지 모르는" 자리에서 판을 통째로 결정짓는 유물이 나오면
    /// 선택이 도박이 되어 버리므로, 그런 자리에서는 이쪽을 쓴다.
    /// </summary>
    public RelicData DrawNextNonChoice() => Draw(r => r.IsChoiceItem);

    /// <summary>고르지 않은 유물을 더미로 돌려보낸다 (다우징머신). 사라지지 않고 나중에 다시 나온다.</summary>
    public void ReturnToPool(RelicData relic)
    {
        if (relic == null || relics.Contains(relic) || upcoming.Contains(relic)) return;
        upcoming.Insert(UnityEngine.Random.Range(0, upcoming.Count + 1), relic);
    }

    /// <summary>
    /// 보상으로 유물 하나를 지급한다. <paramref name="fixedRelic"/>이 지정돼 있고 아직 없는
    /// 유물이면 그것을, 아니면 등장 순서에서 다음 유물을 준다. 줄 유물이 없으면 아무 일도 없다.
    /// </summary>
    public static void GrantReward(RelicData fixedRelic) => GrantRewardAndReturn(fixedRelic);

    /// <summary><see cref="GrantReward"/>와 같되 실제로 준 유물을 돌려준다. 없으면 null.</summary>
    public static RelicData GrantRewardAndReturn(RelicData fixedRelic)
    {
        RelicManager manager = Instance;
        if (manager == null) return null;

        RelicData relic = fixedRelic != null && !manager.Has(fixedRelic)
            ? fixedRelic
            : manager.DrawNext();
        if (relic == null) return null;
        manager.AddRelic(relic);
        return relic;
    }

    /// <summary>구애 시리즈를 뺀 유물 하나를 준다. 실제로 준 유물을 돌려준다(없으면 null).</summary>
    public static RelicData GrantNonChoiceReward()
    {
        RelicManager manager = Instance;
        if (manager == null) return null;
        RelicData relic = manager.DrawNextNonChoice();
        if (relic == null) return null;
        manager.AddRelic(relic);
        return relic;
    }

    /// <summary>
    /// 보스방 보상으로 <b>내놓을</b> 유물을 뽑는다. 지급하지는 않는다 —
    /// 다우징머신이 있으면 둘(고르게 한다), 없으면 하나. 줄 것이 없으면 빈 목록이다.
    ///
    /// 뽑기와 지급을 나눈 이유: 보스 보상은 <see cref="BossRewardSequence"/>가 순서를 정해
    /// 한 장씩 보여 주는데, 그러려면 "무엇이 나왔는지"를 화면에 그리기 <i>전에</i> 알아야 한다.
    /// </summary>
    public static List<RelicData> DrawBossReward(RelicData fixedRelic)
    {
        RelicManager manager = Instance;
        if (manager == null) return new List<RelicData>();

        // 방이 특정 유물을 못박아 두었으면 더미를 거치지 않는다 (이미 지녔으면 더미에서 뽑는다).
        if (fixedRelic != null && !manager.Has(fixedRelic))
            return new List<RelicData> { fixedRelic };

        return manager.DrawNext(manager.Has(RelicEffect.DowsingMachine) ? 2 : 1);
    }

    /// <summary>
    /// 다른 전체 화면 연출이 끝날 때까지 기다린다.
    ///
    /// 보스방 클리어는 "보상 유물 지급 → 진화 연출" 순서라, 보상이 창을 띄우는 유물이면
    /// 진화 컷씬과 겹친다. 둘 다 <see cref="Time.timeScale"/>을 건드리므로 겹치면 시간이
    /// 0으로 굳은 채 남는다. 한 프레임을 먼저 흘려보내는 것이 중요한데, 진화는 같은 프레임의
    /// <i>뒤에</i> 시작되어 지금 당장은 IsEvolving이 아직 false이기 때문이다.
    /// </summary>
    private IEnumerator WaitForQuietScreen()
    {
        yield return null;
        while (MoveUpgradePanel.IsOpen || EventDialogue.IsOpen || RelicChoicePanel.IsOpen ||
               BossRewardSequence.IsRunning || IsEvolutionPlaying)
            yield return null;
    }

    private bool IsEvolutionPlaying
    {
        get
        {
            if (cachedEvolution == null) cachedEvolution = FindAnyObjectByType<PlayerEvolution>();
            return cachedEvolution != null && cachedEvolution.IsEvolving;
        }
    }

    /// <summary>
    /// 유물을 지급한다. 이미 가진 유물이거나, 구애 시리즈를 이미 하나 지니고 있으면 아무 일도 하지 않는다.
    /// </summary>
    public void AddRelic(RelicData relic) => AddRelic(relic, true);

    /// <summary>
    /// <paramref name="announce"/>가 false면 획득 팝업을 띄우지 않는다.
    /// 보스 보상 흐름처럼 <b>이미 전용 화면으로 보여 준</b> 자리에서 쓴다 — 팝업까지 겹치면
    /// 같은 유물이 화면 두 곳에 동시에 뜬다.
    /// </summary>
    public void AddRelic(RelicData relic, bool announce)
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

        if (announce && UIManager.Instance != null)
            UIManager.Instance.ShowRelicAcquired(relic, 3f);

        ApplyOnAcquire(relic);
    }

    /// <summary>
    /// 소비형 유물을 하나 써 없앤다 (기력의 덩어리). 가지고 있었다면 true.
    /// 다 쓴 유물은 "아직 못 얻은 것"으로 돌아가므로 나중에 다시 나올 수 있다.
    /// </summary>
    public bool TryConsume(RelicEffect effect)
    {
        RelicData found = relics.Find(r => r.effect == effect);
        if (found == null) return false;

        relics.Remove(found);
        RecalculateModifiers();
        OnRelicsChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 지닌 유물에서 배율을 다시 계산한다.
    ///
    /// 증감은 전부 더한다. 곱하면 유물을 모을수록 증가폭이 스스로 부풀어 판이 금세 무너진다.
    /// 구애 시리즈만 예외로, 더한 결과에 마지막으로 곱해진다 — "다른 모든 것을 계산한 뒤에
    /// 반이 된다"는 것이 그 유물의 무게이고, 더하기로 섞으면 그 무게가 사라진다.
    /// </summary>
    private void RecalculateModifiers()
    {
        float goldBonus = 0f;
        float meleeBonus = 0f;
        float rangedBonus = 0f;
        float speedBonus = 0f;
        float healBonus = 0f;
        float maxHealthBonus = 0f;
        float attackSizeBonus = 0f;
        float cooldownReduction = 0f;
        float zoneDurationBonus = 0f;

        // 구애 시리즈는 하나만 지닐 수 있지만, 계산은 곱으로 두어도 결과가 같다.
        float choiceMelee = 1f;
        float choiceRanged = 1f;
        float choiceSpeed = 1f;

        foreach (RelicData relic in relics)
        {
            switch (relic.effect)
            {
                case RelicEffect.AmuletCoin: goldBonus += amuletCoinGoldBonus; break;
                case RelicEffect.BigRoot: healBonus += bigRootHealBonus; break;
                case RelicEffect.WideLens: attackSizeBonus += wideLensSizeBonus; break;
                case RelicEffect.QuickClaw: cooldownReduction += quickClawCooldownReduction; break;
                case RelicEffect.LightClay: zoneDurationBonus += lightClayZoneDurationBonus; break;
                case RelicEffect.HpUp: maxHealthBonus += hpUpMaxHealthBonus; break;
                case RelicEffect.Protein: meleeBonus += proteinMeleeBonus; break;
                case RelicEffect.Calcium: rangedBonus += calciumRangedBonus; break;
                case RelicEffect.Carbos: speedBonus += carbosMoveSpeedBonus; break;

                case RelicEffect.LifeOrb:
                    meleeBonus += lifeOrbDamageBonus;
                    rangedBonus += lifeOrbDamageBonus;
                    maxHealthBonus -= lifeOrbMaxHealthPenalty;
                    break;

                case RelicEffect.ChoiceBand:
                    choiceMelee *= 1f + choiceBonus;
                    choiceRanged *= choicePenalty;
                    break;
                case RelicEffect.ChoiceSpecs:
                    choiceRanged *= 1f + choiceBonus;
                    choiceMelee *= choicePenalty;
                    break;
                case RelicEffect.ChoiceScarf:
                    choiceSpeed *= 1f + choiceScarfSpeedBonus;
                    choiceMelee *= choiceScarfDamage;
                    choiceRanged *= choiceScarfDamage;
                    break;
            }
        }

        GoldMultiplier = 1f + goldBonus;
        MeleeDamageMultiplier = (1f + meleeBonus) * choiceMelee;
        RangedDamageMultiplier = (1f + rangedBonus) * choiceRanged;
        MoveSpeedMultiplier = (1f + speedBonus) * choiceSpeed;
        HealMultiplier = 1f + healBonus;
        // 최대 체력이 0 이하가 되면 계산이 무너진다. 생명의구슬을 여러 개 겹칠 수는 없지만
        // 수치를 만지다 실수하면 바로 터지는 자리라 여기서 바닥을 둔다.
        MaxHealthMultiplier = Mathf.Max(0.1f, 1f + maxHealthBonus);
        AttackSizeMultiplier = Mathf.Max(0.1f, 1f + attackSizeBonus);
        CooldownMultiplier = Mathf.Max(0.05f, 1f - cooldownReduction);
        ZoneDurationMultiplier = Mathf.Max(0.1f, 1f + zoneDurationBonus);
    }

    private void ApplyOnAcquire(RelicData relic)
    {
        switch (relic.effect)
        {
            // 금구슬: 획득 즉시 골드. 부적금화의 획득량 배율도 함께 탄다.
            case RelicEffect.Nugget:
                if (RunManager.Instance != null) RunManager.Instance.AddGold(nuggetGold);
                break;

            // 이상한사탕: 획득 즉시 기술 강화를 한 번 고르게 한다.
            case RelicEffect.RareCandy:
                StartCoroutine(RareCandyRoutine());
                break;

            // 행복의알은 획득 시점에 아무 일도 하지 않는다. 상점방을 나갈 때
            // RoomFlowController가 진화를 앞당긴다.
            // 기력의 덩어리도 마찬가지로, 쓰러졌을 때 PlayerDeathHandler가 소비한다.
        }
    }

    private IEnumerator RareCandyRoutine()
    {
        yield return WaitForQuietScreen();

        PlayerMoves moves = PlayerMoves.Instance;
        bool opened = moves != null && UIManager.Instance != null &&
                      UIManager.Instance.ShowMoveUpgrades(moves);

        // 더 강화할 기술이 없으면 조용히 넘어간다. 아무 안내도 없으면 먹통처럼 보이므로 알린다.
        if (!opened && UIManager.Instance != null)
            UIManager.Instance.ShowMessage("이상한사탕을 삼켰지만 더 강해질 기술이 없었다...", 2.5f);
    }

}
