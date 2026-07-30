using UnityEngine;

/// <summary>
/// 방 양쪽 통로를 막는 구름 두 덩이를 들고 있는다. 방이 생길 때 <see cref="RoomFlowController"/>가
/// 방 안에 하나 만들어 붙이고, 방이 지워지면 같이 사라진다.
///
/// 방마다 프리팹에 구름을 심지 않는 이유: 스물한 방의 크기와 통로 자리를 모두 같게 맞춰
/// 두었으므로(<c>RoomFormatUnify</c>), 자리를 코드로 계산하는 편이 방마다 심어 두는 것보다
/// 어긋날 여지가 없다. 방 하나를 손보고 나머지 스무 개를 잊는 일이 생기지 않는다.
///
/// 오른쪽은 싸움이 끝나면 스스로 열린다. 왼쪽은 들어오는 연출이 직접 여닫는다.
/// </summary>
public class RoomGates : MonoBehaviour
{
    /// <summary>지금 방의 구름. 방이 바뀌면 새 것으로 갈린다.</summary>
    public static RoomGates Current { get; private set; }

    /// <summary>통로 입구의 x. 방 벽 바로 밖이다 (벽 안쪽 면이 ±7, 벽 두께 0.5).</summary>
    private const float MouthX = 8f;

    /// <summary>통로 입구 크기. 높이 2칸은 통로 두 줄과 같고, 폭은 넉넉히 덮는다.</summary>
    private static readonly Vector2 MouthSize = new Vector2(1.6f, 2f);

    public CorridorCloud Left { get; private set; }
    public CorridorCloud Right { get; private set; }

    /// <summary>방 안에 구름을 세운다.</summary>
    public static RoomGates Create(Transform room, Sprite[] frames)
    {
        var go = new GameObject("RoomGates");
        go.transform.SetParent(room, false);
        var gates = go.AddComponent<RoomGates>();

        gates.Left = CorridorCloud.Create(go.transform, "Cloud_Left",
            new Vector2(-MouthX, 0f), MouthSize, frames);
        gates.Right = CorridorCloud.Create(go.transform, "Cloud_Right",
            new Vector2(MouthX, 0f), MouthSize, frames);

        // 둘 다 막힌 채로 시작한다. 들어오는 연출이 왼쪽을 잠시 열어 준다.
        gates.Left.SetOpenImmediate(false);
        gates.Right.SetOpenImmediate(false);

        Current = gates;
        return gates;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void Update()
    {
        // 싸움이 남아 있지 않으면 오른쪽이 열린다. 전투방이 아닌 방(상점·이벤트)은
        // CombatActive가 처음부터 거짓이라 들어서자마자 열린다.
        if (Right != null && !Right.IsOpen && !CombatRoomController.CombatActive)
            Right.Open();
    }
}
