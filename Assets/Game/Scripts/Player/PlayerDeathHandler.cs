using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 사망 처리: 기력의 덩어리가 있으면 방 진입 전 상태(입구, 체력 회복)로 부활,
/// 없으면 게임 오버를 띄우고 R 키로 재시작한다.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerController))]
public class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] private Vector2 roomEntrance = new Vector2(-7f, 0f);

    private Health health;
    private PlayerController controller;
    private bool isGameOver;

    private void Awake()
    {
        health = GetComponent<Health>();
        controller = GetComponent<PlayerController>();
        health.OnDied += HandleDeath;
    }

    private void HandleDeath()
    {
        // 기력의 덩어리는 1회 소비형이다. 쓰고 나면 목록에서 사라진다.
        if (RelicManager.Instance != null && RelicManager.Instance.TryConsume(RelicEffect.EnergyRoot))
        {
            StartCoroutine(ReviveRoutine());
            return;
        }

        isGameOver = true;
        if (UIManager.Instance == null) return;

        int floor = RoomFlowController.Instance != null ? RoomFlowController.Instance.CurrentFloorIndex + 1 : 1;
        int gold = RunManager.Instance != null ? RunManager.Instance.Gold : 0;
        UIManager.Instance.ShowMessage(
            "쓰러졌다...  " + floor + "층에서 여정 종료  ·  획득 골드 " + gold + "G\nR : 다시 시작", 9999f);
    }

    private IEnumerator ReviveRoutine()
    {
        // 실제 시간으로 센다. 보스의 마지막 공격과 함께 쓰러지면 곧바로 보스 보상 흐름이
        // 시간을 멈추는데, 스케일 시간으로 기다리면 부활이 영영 오지 않는다.
        yield return new WaitForSecondsRealtime(0.8f);

        // 방 진입 전 상태로 복원: 입구 위치 + 체력 전부 회복
        transform.position = roomEntrance;
        health.Revive(health.MaxHealth);
        controller.ControlEnabled = true;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage("기력의 덩어리를 씹어 삼키고 다시 일어섰다!", 2.5f);
    }

    private void Update()
    {
        if (!isGameOver) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
