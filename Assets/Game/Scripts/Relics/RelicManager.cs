using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 획득한 유물을 보관하고 효과를 발동한다. 방이 바뀌어도 유지되며
/// 씬을 새로 로드(새 게임)하면 초기화된다.
/// </summary>
public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    private readonly List<RelicData> relics = new List<RelicData>();

    public IReadOnlyList<RelicData> Relics => relics;

    public event Action OnRelicsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool Has(RelicEffect effect)
    {
        return relics.Exists(r => r.effect == effect);
    }

    public void AddRelic(RelicData relic)
    {
        if (relic == null) return;
        relics.Add(relic);
        OnRelicsChanged?.Invoke();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage("유물 획득 — " + relic.relicName + ": " + relic.description, 3f);

        ApplyOnAcquire(relic);
    }

    /// <summary>소비형 유물을 하나 제거한다. 있었다면 true.</summary>
    public bool TryConsume(RelicEffect effect)
    {
        RelicData found = relics.Find(r => r.effect == effect);
        if (found == null) return false;
        relics.Remove(found);
        OnRelicsChanged?.Invoke();
        return true;
    }

    private void ApplyOnAcquire(RelicData relic)
    {
        // 행복의알: 획득 즉시(=보스방 진입 전에) 다음 단계로 진화한다.
        if (relic.effect == RelicEffect.HappyEgg)
        {
            PlayerEvolution evolution = FindAnyObjectByType<PlayerEvolution>();
            if (evolution != null) evolution.Evolve();
        }
    }
}
