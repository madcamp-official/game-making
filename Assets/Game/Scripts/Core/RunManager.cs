using System;
using UnityEngine;

/// <summary>
/// 한 판(런) 동안 유지되는 상태: 재화 등.
/// 방 프리팹이 교체되어도 씬 최상위 오브젝트라 그대로 유지된다.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    public int Gold { get; private set; }

    public event Action<int> OnGoldChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>골드를 준다. 부적금화 같은 획득량 배율은 여기서 한 번에 적용된다.</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        // 결과 화면이 "번 골드"를 보여 준다. 쓴 돈은 빼지 않으므로 들어올 때만 센다.
        RunStats.CountGold(amount);

        RelicManager relics = RelicManager.Instance;
        if (relics != null && !Mathf.Approximately(relics.GoldMultiplier, 1f))
            amount = Mathf.Max(1, GameMath.RoundHalfUp(amount * relics.GoldMultiplier));

        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }
}
