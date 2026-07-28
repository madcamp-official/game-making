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

    [Header("보스방 보상 유물 (1·2층)")]
    [Tooltip("비워 두면 유물 등장 순서에서 다음 유물을 준다. 특정 유물을 고정하고 싶을 때만 채운다.")]
    [SerializeField] private RelicData bossRewardRelic;

    private readonly List<Health> aliveEnemies = new List<Health>();

    /// <summary>
    /// 지금 있는 방이 전투방(보스방 포함)인지. 기술은 여기서만 쓸 수 있다.
    ///
    /// 방 종류를 따로 들고 있는 데이터가 없어서, 이 컴포넌트가 붙어 있느냐로 판별한다 —
    /// 전투방과 보스방에만 붙어 있으므로 그 자체가 곧 방 종류다.
    /// </summary>
    public static bool InCombatRoom => activeRooms > 0;

    private static int activeRooms;

    /// <summary>정적 값이라 판이 바뀌어도 살아남는다. 판마다 0에서 시작해야 한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRoomCount() => activeRooms = 0;

    // 방을 옮길 때 옛 방은 프레임 끝에 지워지고 새 방은 곧바로 생긴다. 그래서 잠깐 둘이 겹치는데,
    // 세어 두면 그 사이에도 0으로 떨어지지 않는다.
    private void OnEnable() => activeRooms++;
    private void OnDisable() => activeRooms = Mathf.Max(0, activeRooms - 1);

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

    // 일반 전투방은 클리어해도 아무것도 주지 않는다. 보스방만 보상 유물을 준다.
    // 어느 쪽이든 먹다남은음식이 있으면 방을 정리한 값으로 체력을 조금 회복한다.
    private void GiveClearReward()
    {
        GiveLeftoversHeal();
        if (isBossRoom)
        {
            RelicManager.GrantReward(bossRewardRelic);
            return;
        }

        // 보스방은 진화로 기술을 하나 주므로 경험치까지 얹지 않는다.
        // 일반 전투방 두 개마다 레벨이 올라 강화 팔레트가 뜬다.
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddRoomClear();
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
