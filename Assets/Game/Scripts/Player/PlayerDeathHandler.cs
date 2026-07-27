using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 사망 처리: 게임 오버를 띄우고 R 키로 재시작한다.
///
/// 예전에는 자뭉열매가 1회 부활을 주었지만, 그 유물이 기력의 덩어리(즉시 회복)로 바뀌면서
/// 부활 수단은 사라졌다. 부활을 다시 넣으려면 유물 효과를 추가하고 여기서 분기하면 된다.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDeathHandler : MonoBehaviour
{
    private bool isGameOver;

    private void Awake()
    {
        GetComponent<Health>().OnDied += HandleDeath;
    }

    private void HandleDeath()
    {
        isGameOver = true;
        if (UIManager.Instance == null) return;

        int floor = RoomFlowController.Instance != null ? RoomFlowController.Instance.CurrentFloorIndex + 1 : 1;
        int gold = RunManager.Instance != null ? RunManager.Instance.Gold : 0;
        UIManager.Instance.ShowMessage(
            "쓰러졌다...  " + floor + "층에서 여정 종료  ·  획득 골드 " + gold + "G\nR : 다시 시작", 9999f);
    }

    private void Update()
    {
        if (!isGameOver) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
