using UnityEngine;

/// <summary>
/// 고를 수 있는 캐릭터 한 종. 캐릭터가 <b>코드가 아니라 데이터</b>가 되도록 모아 둔다.
///
/// 예전에는 이상해씨 하나가 씬의 플레이어 오브젝트에 직접 굳어 있었다. 캐릭터를 늘리려면
/// 씬을 복제하거나 분기를 심어야 했다. 이제 고르는 화면은 이 목록을 읽고, 게임을 시작할 때
/// 고른 것 하나를 플레이어에게 입힌다 — 캐릭터를 더하는 일이 <b>에셋 하나 만드는 일</b>이 된다.
///
/// 진화 단계는 <see cref="PlayerEvolution"/>이 이미 배열로 들고 있으므로 여기서는 그 배열을
/// 그대로 담아 두고, 게임 시작 때 통째로 갈아 끼운다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Character Data", fileName = "CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("표시")]
    [Tooltip("고르는 화면과 결과 화면에 쓰는 이름.")]
    public string displayName = "이상해씨";

    [Tooltip("플레이 스타일 한 줄. 수치가 아니라 '어떻게 싸우는가'를 적는다 — 수치는 진화·강화·유물로 계속 달라져서, 적어 두면 화면이 거짓말을 한다.")]
    [TextArea(2, 3)]
    public string playStyle = "안정적인 근거리·범위 공격";

    [Tooltip("고르는 화면에 세울 그림. 진화 1단계의 남쪽 첫 프레임을 쓴다.")]
    public Sprite portrait;

    [Tooltip("고르는 화면에서 이 캐릭터가 서 있는 자리에 재생할 컨트롤러. 마우스를 올리면 걷기 시작한다.")]
    public RuntimeAnimatorController previewController;

    [Tooltip("마우스를 올렸을 때 재생할 상태 이름. 비우면 강조만 한다.")]
    public string previewHoverState = "Walk_0";

    [Tooltip("가만히 있을 때 재생할 상태 이름.")]
    public string previewIdleState = "Idle_0";

    [Header("게임 진입")]
    [Tooltip("진화 단계 전체. PlayerEvolution의 배열을 그대로 갈아 끼운다.")]
    public PlayerEvolution.Stage[] stages;

    /// <summary>고르는 화면이 비어 보이지 않도록, 그림이 없으면 이름만이라도 쓴다.</summary>
    public bool HasPortrait => portrait != null;
}
