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
    [Tooltip("비워 두면 유물 등장 순서에서 다음 유물을 준다. 특정 유물을 고정하고 싶을 때만 채운다.")]
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
    // 어느 쪽이든 먹다남은음식이 있으면 방을 정리한 값으로 체력을 조금 회복한다.
    private void GiveClearReward()
    {
        GiveLeftoversHeal();

        if (isBossRoom)
        {
            RelicManager.GrantReward(bossRewardRelic);
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
            Health playerHealth = FindPlayerHealth();
            if (playerHealth != null) playerHealth.Heal(clearHealAmount);
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("방 클리어! 체력을 " + clearHealAmount + " 회복했다.", 2f);
        }
    }

    // 먹다남은음식: 전투방을 정리할 때마다 체력을 조금 회복한다.
    private void GiveLeftoversHeal()
    {
        RelicManager relics = RelicManager.Instance;
        if (relics == null || !relics.Has(RelicEffect.Leftovers)) return;
        if (relics.LeftoversHealPerRoom <= 0) return;

        Health playerHealth = FindPlayerHealth();
        if (playerHealth != null) playerHealth.Heal(relics.LeftoversHealPerRoom);
    }

    private static Health FindPlayerHealth()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        return player != null ? player.GetComponent<Health>() : null;
    }
}
