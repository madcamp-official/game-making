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

    /// <summary>
    /// 지금 전투방의 일련번호. 방에 들어설 때마다 올라간다.
    /// "방마다 한 번"인 기술(씨뿌리기)은 이 번호를 기억해 두고 달라졌을 때 다시 채운다 —
    /// 시간으로 재는 쿨타임과 달리, 방을 넘어가는 순간이 곧 기준이 된다.
    /// </summary>
    public static int VisitId { get; private set; }

    /// <summary>
    /// 지금 방에 아직 싸움이 남아 있는지. 전투방 안이면서 적이 살아 있을 때만 참이다.
    ///
    /// "전투방인가"와 나눠 둔 이유: 마지막 적을 잡고 나서도 방은 그대로라, 빈 방에서
    /// 느긋하게 씨뿌리기를 깔아 두고 나갈 수 있었다. 방마다 한 번뿐인 기술이 아무 대가 없는
    /// 회복이 되면 "이 방에서 언제 쓸 것인가"라는 선택이 사라진다.
    /// </summary>
    public static bool CombatActive => InCombatRoom && clearedVisitId != VisitId;

    private static int activeRooms;

    /// <summary>마지막으로 정리가 끝난 방의 번호. 방을 옮기면 <see cref="VisitId"/>가 달라져 저절로 풀린다.</summary>
    private static int clearedVisitId = -1;

    /// <summary>정적 값이라 판이 바뀌어도 살아남는다. 판마다 0에서 시작해야 한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRoomCount()
    {
        activeRooms = 0;
        VisitId = 0;
        clearedVisitId = -1;
    }

    // 새 판을 시작할 때 이 값들을 손으로 되돌릴 필요는 없다. VisitId는 방이 켜질 때마다
    // 단조 증가하므로 새 방이 올라오면 clearedVisitId와 저절로 달라진다. 오히려 판 도중에
    // activeRooms를 0으로 밀면 아직 켜져 있는 방이 나가면서 계수가 어긋난다.

    // 방을 옮길 때 옛 방은 프레임 끝에 지워지고 새 방은 곧바로 생긴다. 그래서 잠깐 둘이 겹치는데,
    // 세어 두면 그 사이에도 0으로 떨어지지 않는다.
    private void OnEnable()
    {
        activeRooms++;
        VisitId++;
    }

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

        if (aliveEnemies.Count == 0) MarkCleared();   // 적을 배치하지 않은 방
        if (exitDoor != null)
            exitDoor.SetOpen(aliveEnemies.Count == 0);
    }

    private void HandleEnemyDied(Health enemyHealth)
    {
        aliveEnemies.Remove(enemyHealth);
        if (aliveEnemies.Count > 0) return;

        MarkCleared();
        GiveLeftoversHeal();

        // 방이 조용해졌다. 곡은 그대로 두고 크기만 낮춘다 — 남은 것을 줍고 나가는 동안
        // 싸울 때와 같은 크기로 계속 울리면 방이 끝났다는 것이 소리로 전해지지 않는다.
        // 적을 배치하지 않은 방(Start에서 곧장 MarkCleared)은 여기를 지나지 않는다.
        // 싸움이 없었으니 잦아들 것도 없다.
        GameAudio.DuckForClearedRoom();

        if (isBossRoom)
        {
            // 진화·기술 습득·유물을 한꺼번에 터뜨리지 않고 한 장씩 보여 준다.
            // 출구도 그 흐름이 다 끝난 뒤에 연다.
            if (BossRewardSequence.Begin(transform, bossRewardRelic, exitDoor)) return;

            // 화면을 띄울 수 없으면 예전 방식으로 즉시 처리한다. 보상을 잃지 않는 것이 먼저다.
            RelicManager.GrantReward(bossRewardRelic);
            PlayerEvolution evolution = FindAnyObjectByType<PlayerEvolution>();
            if (evolution != null) evolution.Evolve();
            if (exitDoor != null) exitDoor.SetOpen(true);
            return;
        }

        // 일반 전투방은 스테이지 클리어 글씨 → 강화 선택 → 출구 개방 순서로 마무리한다.
        // 경험치도 그 흐름 안에서 얹는다 — 여기서 얹으면 글씨보다 팔레트가 먼저 뜬다.
        //
        // 보스방이 이 길로 오지 않는 것이 중요하다. 그쪽은 진화로 기술을 하나 주므로
        // 경험치까지 얹으면 보스 한 번에 강화를 두 번 받는다.
        RoomClearSequence.Begin(exitDoor);
    }

    /// <summary>
    /// 싸움이 끝났다고 표시하고, 적이 남긴 흔적을 걷어 낸다.
    ///
    /// 마지막 적이 죽어도 이미 깔린 독장판이나 날아가던 뼈다귀는 그대로 남아, 아무도 없는
    /// 방을 가로지르다 얻어맞는 일이 있었다. 때린 주인이 없어진 공격은 더 배울 것이 없으므로
    /// 함께 치운다.
    /// </summary>
    private void MarkCleared()
    {
        clearedVisitId = VisitId;
        EnemyEffect.ClearUnder(transform);
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
