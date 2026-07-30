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

    /// <summary>
    /// 부품을 손에 쥔다. <b>이미 쥐고 있으면 아무 일도 하지 않고, 아직이면 여기서 잡는다.</b>
    /// 밖에서 들어오는 모든 길의 첫 줄에 둔다 — <see cref="Awake"/>가 이미 돌았다고 믿으면 안 된다.
    ///
    /// 판을 새로 깔 때 <see cref="GameFlow"/>가 이 컴포넌트를 찾는 길만 유별나다. 다른 초기화
    /// 대상(<c>RunManager</c>·<c>PlayerLevel</c>·<c>PlayerMoves</c>·<c>RelicManager</c>)은 모두
    /// 정적 <c>Instance</c>로 찾는데, 그 값은 <c>Awake</c>에서 채워지므로 <b>Awake 전이면 null이라
    /// 저절로 걸러진다</b>. 이곳만 <c>FindAnyObjectByType</c>으로 찾아서, 아직 깨어나지 않은
    /// 오브젝트도 그대로 손에 들어온다. 그때 캐시를 그냥 믿으면 NullReferenceException이 난다.
    ///
    /// 재생 중에는 모든 <c>Awake</c>가 첫 <c>Start</c>보다 먼저 끝나므로 실제로 걸릴 일이 드물지만,
    /// 에디터에서 <see cref="GameFlow.BeginRun"/>을 직접 부르면 (재생 모드가 재컴파일로 풀린 것을
    /// 눈치채지 못한 채 부르는 것이 흔하다) 곧바로 이 상황이 된다. 공개 함수가 생명주기 순서에
    /// 기대지 않게 두는 편이 값싸다.
    /// </summary>
    private void EnsureParts()
    {
        if (health == null) health = GetComponent<Health>();
        if (controller == null) controller = GetComponent<PlayerController>();
    }

    private void Awake()
    {
        EnsureParts();
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

        // 결과 화면이 있으면 그쪽이 마무리를 맡는다. 옛 안내문(R로 재시작)은 화면이 없을
        // 때를 위한 대비로 남겨 둔다 — 보상을 잃는 것보다 촌스러운 편이 낫다.
        if (GameFlow.Instance != null) { GameFlow.Instance.FinishRun(false); return; }

        if (UIManager.Instance == null) return;

        int floor = RoomFlowController.Instance != null ? RoomFlowController.Instance.CurrentFloorIndex + 1 : 1;
        int gold = RunManager.Instance != null ? RunManager.Instance.Gold : 0;
        UIManager.Instance.ShowMessage(
            "쓰러졌다...  " + floor + "층에서 여정 종료  ·  획득 골드 " + gold + "G\nR : 다시 시작", 9999f);
    }

    /// <summary>
    /// 새 판을 시작한다. 쓰러진 상태를 통째로 걷어낸다 — 체력을 가득 채우고, 사망으로 꺼 둔
    /// 조작을 되살리고, 게임 오버 표시를 내린다.
    ///
    /// 결과 화면에서 다시 시작하면 <b>씬을 다시 올리지 않는다.</b> 그래서 죽은 몸이 그대로
    /// 다음 판으로 넘어간다 — 체력 0에 조작도 꺼진 채로 시작하던 것이 이 때문이다.
    /// </summary>
    public void ResetForNewRun()
    {
        EnsureParts();
        isGameOver = false;
        // 진화 단계를 먼저 입힌 뒤에 불러야 그 단계의 최대치로 찬다 (GameFlow가 순서를 지킨다).
        health.Revive(health.MaxHealth);
        controller.ControlEnabled = true;
        transform.position = roomEntrance;
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
