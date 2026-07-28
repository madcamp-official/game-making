using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배운 기술과 그 강화 상태를 들고 있는 곳. 실제 공격은 <see cref="PlayerCombat"/>가 하고,
/// 여기서 나오는 배율을 곱해 쓴다.
///
/// 기술은 처음에 둘(몸통박치기·덩굴채찍)로 시작해 진화할 때마다 하나씩 늘어난다.
/// 강화는 한 기술당 최대 두 번이고, 한 번 고른 선택지는 다시 나오지 않는다.
/// </summary>
public class PlayerMoves : MonoBehaviour
{
    public static PlayerMoves Instance { get; private set; }

    /// <summary>기술을 배우거나 강화했을 때. HUD가 다시 그린다.</summary>
    public event Action OnMovesChanged;

    private readonly List<MoveType> learned = new List<MoveType>();
    private readonly Dictionary<MoveType, int> upgradeCounts = new Dictionary<MoveType, int>();
    private readonly HashSet<MoveUpgradeId> taken = new HashSet<MoveUpgradeId>();

    // 강화로 쌓이는 배율. 곱셈으로 누적된다.
    public float TackleDamageMultiplier { get; private set; } = 1f;
    public float TackleCooldownMultiplier { get; private set; } = 1f;
    /// <summary>공격 중 이속 "감소량"에 곱하는 값. 1이면 원래대로, 작을수록 덜 느려진다.</summary>
    public float TackleSlowReductionMultiplier { get; private set; } = 1f;
    public float VineRangeMultiplier { get; private set; } = 1f;
    public float VineStunMultiplier { get; private set; } = 1f;
    public float VineCooldownMultiplier { get; private set; } = 1f;

    // 장판 계열. 회복량과 지속시간은 명세가 더할 값을 못박아 두어 배율이 아니라 덧셈으로 쌓인다.
    public int SeedHealBonus { get; private set; }
    public float SeedDurationBonus { get; private set; }
    public float SeedRadiusMultiplier { get; private set; } = 1f;
    public float PetalRadiusMultiplier { get; private set; } = 1f;
    public float PetalDamageMultiplier { get; private set; } = 1f;
    public float PetalDurationBonus { get; private set; }

    public IReadOnlyList<MoveType> Learned => learned;

    public bool Has(MoveType move) => learned.Contains(move);

    public int UpgradeCount(MoveType move) =>
        upgradeCounts.TryGetValue(move, out int count) ? count : 0;

    public bool CanUpgrade(MoveType move) =>
        Has(move) && UpgradeCount(move) < MoveInfo.MaxUpgradesPerMove;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        for (int i = 0; i < MoveInfo.StartingMoveCount && i < MoveInfo.LearnOrder.Length; i++)
            learned.Add(MoveInfo.LearnOrder[i]);
    }

    private void Start()
    {
        // 레벨은 Awake에서 자리를 잡으므로 Start에서 붙어야 놓치지 않는다.
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.OnLevelUp += HandleLevelUp;
    }

    private void OnDestroy()
    {
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.OnLevelUp -= HandleLevelUp;
        if (Instance == this) Instance = null;
    }

    /// <summary>레벨이 오르면 강화 팔레트를 띄운다. 남은 선택지가 없으면 조용히 넘어간다.</summary>
    private void HandleLevelUp()
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowMoveUpgrades(this);
    }

    /// <summary>진화할 때 다음 기술을 하나 배운다. 더 배울 게 없으면 아무 일도 없다.</summary>
    public void LearnNext()
    {
        if (learned.Count >= MoveInfo.MaxMoves) return;
        foreach (MoveType move in MoveInfo.LearnOrder)
        {
            if (learned.Contains(move)) continue;
            learned.Add(move);
            OnMovesChanged?.Invoke();
            if (UIManager.Instance != null)
            {
                string name = MoveInfo.NameOf(move);
                UIManager.Instance.ShowMessage(
                    "새로운 기술 " + name + KoreanText.ObjectParticle(name) + " 배웠다!", 2.5f);
            }
            return;
        }
    }

    /// <summary>
    /// 지금 제시할 수 있는 강화 선택지. 배우지 않은 기술, 이미 두 번 강화한 기술,
    /// 한 번 고른 선택지는 빠진다.
    /// </summary>
    public List<MoveUpgradeOption> AvailableUpgrades()
    {
        List<MoveUpgradeOption> result = new List<MoveUpgradeOption>();
        foreach (MoveUpgradeOption option in MoveUpgrades.All)
        {
            if (taken.Contains(option.id)) continue;
            if (!CanUpgrade(option.move)) continue;
            result.Add(option);
        }
        return result;
    }

    /// <summary>
    /// 무작위로 최대 <paramref name="count"/>개를 뽑는다. 남은 게 적으면 그만큼만 준다.
    /// 고르지 않은 선택지는 다음에 다시 나올 수 있으므로 여기서는 아무것도 소비하지 않는다.
    /// </summary>
    public List<MoveUpgradeOption> RollUpgrades(int count)
    {
        List<MoveUpgradeOption> pool = AvailableUpgrades();
        List<MoveUpgradeOption> picked = new List<MoveUpgradeOption>();
        while (picked.Count < count && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return picked;
    }

    /// <summary>선택지를 확정한다. 이때부터 그 선택지는 다시 나오지 않는다.</summary>
    public void ApplyUpgrade(MoveUpgradeId id)
    {
        if (taken.Contains(id)) return;

        MoveUpgradeOption option = default;
        bool found = false;
        foreach (MoveUpgradeOption candidate in MoveUpgrades.All)
            if (candidate.id == id) { option = candidate; found = true; break; }
        if (!found || !CanUpgrade(option.move)) return;

        taken.Add(id);
        upgradeCounts[option.move] = UpgradeCount(option.move) + 1;

        switch (id)
        {
            case MoveUpgradeId.TackleDamage:
                TackleDamageMultiplier *= MoveUpgrades.DamageStep; break;
            case MoveUpgradeId.TackleSlow:
                TackleSlowReductionMultiplier *= MoveUpgrades.SlowStep; break;
            case MoveUpgradeId.TackleSpeed:
                TackleCooldownMultiplier *= MoveUpgrades.SpeedStep; break;
            case MoveUpgradeId.VineRange:
                VineRangeMultiplier *= MoveUpgrades.RangeStep; break;
            case MoveUpgradeId.VineStun:
                VineStunMultiplier *= MoveUpgrades.StunStep; break;
            case MoveUpgradeId.VineCooldown:
                VineCooldownMultiplier *= MoveUpgrades.CooldownStep; break;
            case MoveUpgradeId.SeedHeal:
                SeedHealBonus += MoveUpgrades.SeedHealStep; break;
            case MoveUpgradeId.SeedDuration:
                SeedDurationBonus += MoveUpgrades.SeedDurationStep; break;
            case MoveUpgradeId.SeedRadius:
                SeedRadiusMultiplier *= MoveUpgrades.SeedRadiusStep; break;
            case MoveUpgradeId.PetalRadius:
                PetalRadiusMultiplier *= MoveUpgrades.PetalRadiusStep; break;
            case MoveUpgradeId.PetalDamage:
                PetalDamageMultiplier *= MoveUpgrades.PetalDamageStep; break;
            case MoveUpgradeId.PetalDuration:
                PetalDurationBonus += MoveUpgrades.PetalDurationStep; break;
        }

        OnMovesChanged?.Invoke();
    }
}
