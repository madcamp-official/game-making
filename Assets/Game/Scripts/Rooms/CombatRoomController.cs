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

        if (exitDoor != null) exitDoor.SetOpen(true);

        if (isBossRoom)
        {
            PlayerEvolution evolution = FindAnyObjectByType<PlayerEvolution>();
            if (evolution != null) evolution.Evolve();
        }
    }
}
