using System;
using UnityEngine;

/// <summary>
/// 경험치와 레벨. 전투방을 하나 치울 때마다 경험치 바가 절반씩 차고, 가득 차면 레벨이 오른다.
/// 즉 전투방 두 개마다 기술 강화 팔레트가 한 번 뜬다.
///
/// 레벨 숫자는 화면에 띄우지 않는다. 플레이어가 알아야 하는 건 "다음 강화까지 얼마나 남았나"뿐이다.
/// </summary>
public class PlayerLevel : MonoBehaviour
{
    public static PlayerLevel Instance { get; private set; }

    [Tooltip("전투방 하나를 치울 때 차오르는 양. 0.5면 두 방마다 레벨이 오른다.")]
    [SerializeField, Range(0.05f, 1f)] private float gainPerRoom = 0.5f;

    /// <summary>다음 레벨까지의 진행도 0~1.</summary>
    public float Progress01 { get; private set; }

    /// <summary>지금 레벨. 표시용이 아니라 기록용이다.</summary>
    public int Level { get; private set; } = 1;

    public event Action OnProgressChanged;
    public event Action OnLevelUp;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>새 판을 시작한다. 씬을 다시 올리지 않으므로 여기서 직접 되돌린다.</summary>
    public void ResetForNewRun()
    {
        Level = 1;
        Progress01 = 0f;
        OnProgressChanged?.Invoke();
    }

    /// <summary>전투방 하나를 치웠다. 보스방은 진화로 기술을 주므로 여기서 세지 않는다.</summary>
    public void AddRoomClear()
    {
        Progress01 += gainPerRoom;
        // 한 번에 여러 레벨이 오를 수도 있으므로 while로 돈다.
        while (Progress01 >= 1f)
        {
            Progress01 -= 1f;
            Level++;
            OnLevelUp?.Invoke();
        }
        OnProgressChanged?.Invoke();
    }
}
