using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어가 조작할 수 없는 동안 적도 멈춘다.
///
/// 방을 넘어갈 때 <see cref="RoomTransition"/>이 조작을 끄고 주인공을 왼쪽 통로에서
/// 걸어 들어오게 하는데, 그 1.4초 남짓 동안 새 방의 적들은 이미 살아 움직였다.
/// 걸어 들어오는 것을 보고만 있어야 하는 사이에 달려들어 때리므로,
/// <b>방에 들어서자마자 맞고 시작하는 판</b>이 나왔다. 피할 방법이 없는 피해다.
///
/// 조작 가능 여부로 재는 이유: 이 상황을 "방 이동 연출 중"으로 좁혀 잡으면 나중에
/// 조작을 끄는 연출이 하나 더 생길 때마다 여기를 고쳐야 한다. <b>손을 쓸 수 없으면
/// 맞지도 않는다</b>가 지키고 싶은 규칙이므로, 그 규칙을 그대로 조건으로 쓴다.
///
/// 경직(<c>PlayerController.Stun</c>)은 여기에 해당하지 않는다. 그쪽은 조작을 끄는 것이
/// 아니라 잠깐 못 움직이는 것이고, 얻어맞은 뒤 적이 멈춰 준다면 싸움이 되지 않는다.
/// </summary>
public static class CombatFreeze
{
    private static PlayerController player;

    /// <summary>지금 적이 멈춰 있어야 하는지.</summary>
    public static bool Active
    {
        get
        {
            // 판을 다시 시작하면 예전 플레이어가 지워질 수 있으므로 없어졌으면 다시 찾는다.
            if (player == null) player = Object.FindAnyObjectByType<PlayerController>();
            // 아직 주인공이 없는 순간까지 멈춰 세우지는 않는다. 없으면 때릴 대상도 없다.
            return player != null && !player.ControlEnabled;
        }
    }

    /// <summary>풀릴 때까지 기다린다. 보스가 전투를 시작하기 전에 한 번 쓴다.</summary>
    public static IEnumerator WaitWhileActive()
    {
        while (Active) yield return null;
    }
}
