using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투방 관리자. 방 안(자기 자식)의 적을 추적하고 전부 죽으면 출구를 활성화한다.
/// 보스방이면 클리어 시 플레이어를 진화시킨다.
/// </summary>
public class CombatRoomController : MonoBehaviour
{
    [SerializeField] private ExitDoor exitDoor;
    [SerializeField] private bool isBossRoom;

    [Header("일반 전투방 클리어 보상 (재화 또는 회복 중 무작위)")]
    [SerializeField, Min(0)] private int clearGoldReward = 5;
    [SerializeField, Min(0)] private int clearHealAmount = 20;

    [Header("보스방 보상 유물 (1·2층)")]
    [SerializeField] private RelicData bossRewardRelic;

    private readonly List<Health> aliveEnemies = new List<Health>();

    private void Start()
    {
        foreach (EnemyController enemy in GetComponentsInChildren<EnemyController>())
        {
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;

            aliveEnemies.Add(enemyHealth);
            enemyHealth.OnDied += () => HandleEnemyDied(enemyHealth);
        }

        if (exitDoor != null)
            exitDoor.SetOpen(aliveEnemies.Count == 0);
    }

    private void HandleEnemyDied(Health enemyHealth)
    {
        aliveEnemies.Remove(enemyHealth);
        if (aliveEnemies.Count > 0) return;

        GiveClearReward();
        if (exitDoor != null) exitDoor.SetOpen(true);

        if (isBossRoom)
        {
            PlayerEvolution evolution = FindAnyObjectByType<PlayerEvolution>();
            if (evolution != null) evolution.Evolve();
        }
    }

    // 일반 전투방: 재화 또는 회복 중 하나. 보스방: 보상 유물.
    private void GiveClearReward()
    {
        if (isBossRoom)
        {
            if (bossRewardRelic != null && RelicManager.Instance != null)
                RelicManager.Instance.AddRelic(bossRewardRelic);
            return;
        }

        if (Random.value < 0.5f && clearGoldReward > 0)
        {
            if (RunManager.Instance != null) RunManager.Instance.AddGold(clearGoldReward);
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("방 클리어! 보상으로 " + clearGoldReward + "G를 얻었다.", 2f);
        }
        else if (clearHealAmount > 0)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            Health playerHealth = player != null ? player.GetComponent<Health>() : null;
            if (playerHealth != null) playerHealth.Heal(clearHealAmount);
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("방 클리어! 체력을 " + clearHealAmount + " 회복했다.", 2f);
        }
    }
}
