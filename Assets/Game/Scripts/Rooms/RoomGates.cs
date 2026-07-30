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

    /// <summary>안개 둑의 중심 x. 방 벽(±7.5)에서 살짝 떨어뜨려 두는 이유: 벽에 붙이면
    /// 둥근 얼굴이 벽 모서리에 걸려 잘린 것처럼 보인다. 통로 안쪽에서 온전한 윤곽으로
    /// 끝나야 "안개가 길을 메웠다"로 읽힌다.</summary>
    private const float BankX = 13.7f;

    /// <summary>안개 둑 크기 — 그림(288×56px, PPU 24) 원본 그대로. 폭 12칸은 카메라
    /// 반폭(10칸)보다 길어 문 앞에 바짝 서도 끝이 보이지 않고, 높이 2.33칸은 통로
    /// 두 줄에 딱 맞아 위아래 나무 타일을 넘보지 않는다.</summary>
    private static readonly Vector2 BankSize = new Vector2(12f, 56f / 24f);

    public CorridorCloud Left { get; private set; }
    public CorridorCloud Right { get; private set; }

    /// <summary>방 안에 구름을 세운다.</summary>
    public static RoomGates Create(Transform room, Sprite sprite)
    {
        var go = new GameObject("RoomGates");
        go.transform.SetParent(room, false);
        var gates = go.AddComponent<RoomGates>();

        // 그림의 둥근 얼굴이 방을 향한다 — 왼쪽 구름은 뒤집는다(faceRight).
        gates.Left = CorridorCloud.Create(go.transform, "Cloud_Left",
            new Vector2(-BankX, 0f), BankSize, sprite, true);
        gates.Right = CorridorCloud.Create(go.transform, "Cloud_Right",
            new Vector2(BankX, 0f), BankSize, sprite, false);

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
