using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기술 칸 하나의 표시 정보와 강화 후보. 실제 효과는 <see cref="PlayerCombat"/>와
/// <see cref="PlayerMoves"/>가 구현한다.
///
/// 기술 이름·설명·속성을 캐릭터 데이터에 둬서, 새 캐릭터를 추가할 때 이상해씨 전용
/// <c>switch</c>를 UI 곳곳에 복제하지 않는다. 배열 순서는 곧 입력 슬롯이자 습득 순서다.
/// </summary>
[Serializable]
public class PlayerMoveDefinition
{
    public MoveType type;
    public string displayName;

    [TextArea(2, 4)]
    public string summary;

    public AttackKind attackKind;

    [Tooltip("비우면 근접/원거리 속성을 표시한다. '방당 1회', '방어'처럼 속성으로 설명되지 않는 기술에 쓴다.")]
    public string tagOverride;

    [Tooltip("이 기술에서 뽑을 수 있는 강화 후보. 한 기술당 실제 획득 한도는 MoveInfo.MaxUpgradesPerMove를 따른다.")]
    public MoveUpgradeId[] upgrades;
}

/// <summary>
/// 캐릭터 한 계열이 쓰는 네 기술. 처음 둘을 가지고 시작하고 진화할 때 배열의 다음 기술을 배운다.
/// </summary>
[Serializable]
public class PlayerMoveSet
{
    [Min(0)] public int startingMoveCount = 2;
    public PlayerMoveDefinition[] moves;

    public int Count => moves != null ? Mathf.Min(moves.Length, MoveInfo.MaxMoves) : 0;
    public int StartingCount => Mathf.Clamp(startingMoveCount, 0, Count);
    public bool IsConfigured => Count > 0;

    public PlayerMoveDefinition DefinitionAt(int index) =>
        index >= 0 && index < Count ? moves[index] : null;

    public int IndexOf(MoveType type)
    {
        for (int i = 0; i < Count; i++)
            if (moves[i] != null && moves[i].type == type) return i;
        return -1;
    }

    public PlayerMoveDefinition Find(MoveType type)
    {
        int index = IndexOf(type);
        return index >= 0 ? moves[index] : null;
    }
}

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

    [Header("구현 상태")]
    [Tooltip("이 캐릭터가 아직 완성되지 않았을 때 실제 플레이에 사용할 캐릭터. 완성되면 비운다.")]
    public CharacterData fallbackCharacter;

    [Header("게임 진입")]
    [Tooltip("이 계열의 기술 네 개. 배열 순서가 좌클릭/우클릭/Shift/Space이자 습득 순서다.")]
    public PlayerMoveSet moveSet = new PlayerMoveSet();

    [Tooltip("진화 단계 전체. PlayerEvolution의 배열을 그대로 갈아 끼운다.")]
    public PlayerEvolution.Stage[] stages;

    /// <summary>고르는 화면이 비어 보이지 않도록, 그림이 없으면 이름만이라도 쓴다.</summary>
    public bool HasPortrait => portrait != null;

    /// <summary>진화 단계와 기술 세트를 모두 가지고 있어 실제로 플레이할 수 있는지.</summary>
    public bool HasOwnImplementation =>
        stages != null && stages.Length > 0 && moveSet != null && moveSet.IsConfigured;

    /// <summary>
    /// 실제 플레이에 사용할 완성 데이터를 찾는다. 폴백이 또 폴백을 가리킬 수 있지만,
    /// 잘못 연결한 순환 참조가 게임을 멈추지 않도록 탐색 횟수를 제한한다.
    /// </summary>
    public CharacterData ResolvePlayable()
    {
        CharacterData current = this;
        for (int i = 0; i < 16 && current != null; i++)
        {
            if (current.HasOwnImplementation) return current;
            current = current.fallbackCharacter;
        }
        return null;
    }

    private void OnValidate()
    {
        if (fallbackCharacter == this)
            Debug.LogError(name + ": fallbackCharacter가 자기 자신을 가리킨다.", this);

        if (moveSet == null || moveSet.moves == null) return;
        if (moveSet.moves.Length > MoveInfo.MaxMoves)
            Debug.LogError(name + ": 기술은 최대 " + MoveInfo.MaxMoves + "개다.", this);

        var seen = new HashSet<MoveType>();
        for (int i = 0; i < moveSet.moves.Length; i++)
        {
            PlayerMoveDefinition move = moveSet.moves[i];
            if (move == null) { Debug.LogError(name + ": 빈 기술 슬롯 " + i, this); continue; }
            if (!seen.Add(move.type)) Debug.LogError(name + ": 중복 기술 " + move.type, this);

            if (move.upgrades == null) continue;
            foreach (MoveUpgradeId id in move.upgrades)
            {
                if (!MoveUpgrades.TryGet(id, out MoveUpgradeOption option) || option.move != move.type)
                    Debug.LogError(name + ": " + move.type + "에 잘못 연결된 강화 " + id, this);
            }
        }
    }
}
