using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배운 기술과 그 강화 상태를 들고 있는 곳. 실제 공격은 <see cref="PlayerCombat"/>가 하고,
/// 여기서 나오는 배율을 곱해 쓴다.
///
/// 기술의 종류와 순서는 선택한 캐릭터의 <see cref="PlayerMoveSet"/>에서 받는다.
/// 이상해씨는 처음에 둘(몸통박치기·덩굴채찍)로 시작해 진화할 때마다 하나씩 늘어난다.
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

    public PlayerMoveSet MoveSet { get; private set; }

    // 강화로 쌓이는 배율. 곱셈으로 누적된다.
    public float TackleDamageMultiplier { get; private set; } = 1f;
    public float TackleCooldownMultiplier { get; private set; } = 1f;
    public float TackleRadiusMultiplier { get; private set; } = 1f;
    public float VineRangeMultiplier { get; private set; } = 1f;
    public float VineSlowDurationMultiplier { get; private set; } = 1f;
    public float VineCooldownMultiplier { get; private set; } = 1f;

    // 장판 계열. 회복량과 지속시간은 명세가 더할 값을 못박아 두어 배율이 아니라 덧셈으로 쌓인다.
    public int SeedHealBonus { get; private set; }
    public float SeedDurationBonus { get; private set; }
    public float SeedRadiusMultiplier { get; private set; } = 1f;
    public float PetalRadiusMultiplier { get; private set; } = 1f;
    public float PetalDamageMultiplier { get; private set; } = 1f;
    public float PetalCooldownMultiplier { get; private set; } = 1f;

    public IReadOnlyList<MoveType> Learned => learned;

    public int MoveCount => MoveSet != null ? MoveSet.Count : 0;

    public MoveType MoveAt(int slot)
    {
        PlayerMoveDefinition definition = MoveSet?.DefinitionAt(slot);
        return definition != null ? definition.type : default;
    }

    public bool Has(MoveType move) => learned.Contains(move);

    /// <summary>특수한 기술 실행부가 선택한 강화의 유무를 직접 확인할 때 쓴다.</summary>
    public bool HasUpgrade(MoveUpgradeId id) => taken.Contains(id);

    public int UpgradeCount(MoveType move) =>
        upgradeCounts.TryGetValue(move, out int count) ? count : 0;

    public bool CanUpgrade(MoveType move) =>
        Has(move) && UpgradeCount(move) < MoveInfo.MaxUpgradesPerMove;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        // 레벨은 Awake에서 자리를 잡으므로 Start에서 붙어야 놓치지 않는다.
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.OnLevelUp += HandleLevelUp;
    }

    /// <summary>
    /// 새 판을 시작한다. 배운 기술과 강화를 전부 버리고 시작 기술만 남긴다.
    /// 씬을 다시 올리지 않고 이어서 도는 구조라 여기서 직접 비워야 한다.
    ///
    /// 배율은 하나씩 되돌리지 않고 <b>전부 기본값으로 다시 적는다</b> — 강화가 늘어날 때
    /// 여기를 같이 고치는 것을 잊으면 지난 판의 배율이 남는다.
    /// </summary>
    public void ResetForNewRun()
    {
        learned.Clear();
        upgradeCounts.Clear();
        taken.Clear();
        if (MoveSet != null)
        {
            for (int i = 0; i < MoveSet.StartingCount; i++)
            {
                PlayerMoveDefinition definition = MoveSet.DefinitionAt(i);
                if (definition != null) learned.Add(definition.type);
            }
        }

        TackleDamageMultiplier = 1f;
        TackleCooldownMultiplier = 1f;
        TackleRadiusMultiplier = 1f;
        VineRangeMultiplier = 1f;
        VineSlowDurationMultiplier = 1f;
        VineCooldownMultiplier = 1f;
        SeedHealBonus = 0;
        SeedDurationBonus = 0f;
        SeedRadiusMultiplier = 1f;
        PetalRadiusMultiplier = 1f;
        PetalDamageMultiplier = 1f;
        PetalCooldownMultiplier = 1f;

        OnMovesChanged?.Invoke();
    }

    /// <summary>
    /// 선택한 캐릭터의 기술 세트를 입히고 새 런 상태로 초기화한다.
    /// 캐릭터를 바꾸지 않고 재도전해도 강화를 반드시 비워야 하므로 설정과 초기화를 한 길로 둔다.
    /// </summary>
    public void LoadMoveSet(PlayerMoveSet moveSet)
    {
        MoveSet = moveSet;
        ResetForNewRun();
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

    /// <summary>
    /// 진화할 때 다음 기술을 하나 배운다. 실제로 배운 기술을 돌려주고, 더 배울 게 없으면 null.
    ///
    /// 돌려주는 이유는 보스 보상 흐름이 "무엇을 배웠는지"를 전용 화면에 적어야 하기 때문이다.
    /// </summary>
    public MoveType? LearnNext()
    {
        if (MoveSet == null || learned.Count >= MoveSet.Count) return null;
        for (int i = 0; i < MoveSet.Count; i++)
        {
            PlayerMoveDefinition definition = MoveSet.DefinitionAt(i);
            if (definition == null) continue;
            MoveType move = definition.type;
            if (learned.Contains(move)) continue;
            learned.Add(move);
            OnMovesChanged?.Invoke();
            // 보스 보상 흐름이 도는 중이면 전용 화면이 이름·조작키·효과까지 따로 안내한다.
            // 여기서 한 줄 알림까지 띄우면 같은 말이 두 번 겹친다.
            if (!BossRewardSequence.IsRunning && UIManager.Instance != null)
            {
                string name = MoveInfo.NameOf(move);
                UIManager.Instance.ShowMessage(
                    "새로운 기술 " + name + KoreanText.ObjectParticle(name) + " 배웠다!", 2.5f);
            }
            return move;
        }
        return null;
    }

    /// <summary>
    /// 지금 제시할 수 있는 강화 선택지. 배우지 않은 기술, 이미 두 번 강화한 기술,
    /// 한 번 고른 선택지는 빠진다.
    /// </summary>
    public List<MoveUpgradeOption> AvailableUpgrades()
    {
        List<MoveUpgradeOption> result = new List<MoveUpgradeOption>();
        if (MoveSet == null) return result;

        for (int moveIndex = 0; moveIndex < MoveSet.Count; moveIndex++)
        {
            PlayerMoveDefinition definition = MoveSet.DefinitionAt(moveIndex);
            if (definition == null || definition.upgrades == null || !CanUpgrade(definition.type)) continue;

            foreach (MoveUpgradeId id in definition.upgrades)
            {
                if (taken.Contains(id)) continue;
                if (!MoveUpgrades.TryGet(id, out MoveUpgradeOption option)) continue;
                // 데이터 연결 실수를 조용히 허용하면 다른 기술의 강화가 표시된다.
                if (option.move != definition.type) continue;
                result.Add(option);
            }
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

        if (!MoveUpgrades.TryGet(id, out MoveUpgradeOption option) || !CanUpgrade(option.move)) return;
        PlayerMoveDefinition definition = MoveSet?.Find(option.move);
        if (definition == null || definition.upgrades == null ||
            System.Array.IndexOf(definition.upgrades, id) < 0) return;

        taken.Add(id);
        upgradeCounts[option.move] = UpgradeCount(option.move) + 1;

        switch (id)
        {
            case MoveUpgradeId.TackleDamage:
                TackleDamageMultiplier *= MoveUpgrades.DamageStep; break;
            case MoveUpgradeId.TackleRadius:
                TackleRadiusMultiplier *= MoveUpgrades.TackleRadiusStep; break;
            case MoveUpgradeId.TackleSpeed:
                TackleCooldownMultiplier *= MoveUpgrades.SpeedStep; break;
            case MoveUpgradeId.VineRange:
                VineRangeMultiplier *= MoveUpgrades.RangeStep; break;
            case MoveUpgradeId.VineSlowDuration:
                VineSlowDurationMultiplier *= MoveUpgrades.VineSlowDurationStep; break;
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
            case MoveUpgradeId.PetalCooldown:
                PetalCooldownMultiplier *= MoveUpgrades.PetalCooldownStep; break;
        }

        OnMovesChanged?.Invoke();
    }
}
