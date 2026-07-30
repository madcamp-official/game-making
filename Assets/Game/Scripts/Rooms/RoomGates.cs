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

    /// <summary>안개 둑의 중심 x. 맨 앞 덩이의 옅은 앞자락이 방 문턱(±7.5) 바로 앞에서
    /// 시작해, 들어서는 순간부터 길이 끝까지 막힌 것으로 보인다. 덩이 자체는 둥글게
    /// 사그라드는 윤곽이라 벽에 닿아도 잘린 것처럼 보이지 않는다.</summary>
    private const float BankX = 13.3f;

    /// <summary>안개 둑 크기 — 그림(288×56px, PPU 24) 원본 그대로. 폭 12칸은 카메라
    /// 반폭(10칸)보다 길어 문 앞에 바짝 서도 끝이 보이지 않고, 높이 2.33칸은 통로
    /// 두 줄에 딱 맞아 위아래 나무 타일을 넘보지 않는다.</summary>
    private static readonly Vector2 BankSize = new Vector2(12f, 56f / 24f);

    /// <summary>
    /// 통로 구멍의 높이. 방 벽(Wall_Right_Top / Wall_Right_Bottom)이 y ±1을 비워 두므로 두 칸이다.
    ///
    /// 구름이 <b>막는</b> 크기는 그림 크기(2.33칸)가 아니라 이 값이어야 한다. 그림은 이음매가
    /// 보이지 않도록 통로보다 넉넉히 덮지만, 몸을 막는 것은 벽이 뚫린 만큼이어야 한다.
    /// </summary>
    private const float CorridorHeight = 2f;

    /// <summary>안개 둑의 바깥 끝. 여기까지 막는다.</summary>
    private static float BankFarX => BankX + BankSize.x * 0.5f;

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
            new Vector2(-BankX, 0f), BankSize, BlockRect(-1f), sprite, true);
        gates.Right = CorridorCloud.Create(go.transform, "Cloud_Right",
            new Vector2(BankX, 0f), BankSize, BlockRect(+1f), sprite, false);

        // 둘 다 막힌 채로 시작한다. 들어오는 연출이 왼쪽을 잠시 열어 준다.
        gates.Left.SetOpenImmediate(false);
        gates.Right.SetOpenImmediate(false);

        Current = gates;
        return gates;
    }

    /// <summary>
    /// 구름이 막을 자리(방 기준). 통로 구멍 <b>바로 앞</b>에서 시작해 안개 끝까지다.
    ///
    /// 안쪽 끝을 벽 안쪽 면(<see cref="RoomArena.HalfSize"/>.x = 7)에 정확히 맞추는 것이 핵심이다.
    /// 그래야 <b>돌아다닐 수 있는 범위가 정확히 네모</b>가 된다 — 위아래 벽도 오른쪽 벽도
    /// 여기서 끝나므로, 통로 앞이라고 조금 더 나가지지도, 조금 덜 나가지지도 않는다.
    ///
    /// 예전에는 이 값이 두 번 어긋났다. 그림 크기에 방 쪽으로 반 칸을 더 내밀었을 때는
    /// 6.7까지 들어와 방 안에 보이지 않는 턱이 생겼고, 그림 폭 그대로 두었을 때는 7.3에서
    /// 시작해 문턱 안쪽에 0.3칸짜리 홈이 남았다.
    /// </summary>
    private static Rect BlockRect(float side)
    {
        float near = RoomArena.HalfSize.x;
        float half = CorridorHeight * 0.5f;
        return side > 0f
            ? Rect.MinMaxRect(near, -half, BankFarX, half)
            : Rect.MinMaxRect(-BankFarX, -half, -near, half);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void Update()
    {
        // 싸움이 남아 있지 않으면 오른쪽이 열린다. 전투방이 아닌 방(상점·이벤트)은
        // CombatActive가 처음부터 거짓이라 들어서자마자 열린다.
        //
        // 단 방 정리 마무리(스테이지 클리어 글씨 → 강화 선택)가 도는 동안에는 닫아 둔다.
        // CombatActive는 마지막 적이 죽는 즉시 거짓이 되는데 — 그래야 뒤늦게 씨뿌리기를
        // 깔 수 없다 — 그 순간 길까지 열리면 강화를 고르다 말고 다음 방으로 나갈 수 있다.
        if (Right != null && !Right.IsOpen &&
            !CombatRoomController.CombatActive &&
            !RoomClearSequence.IsRunning && !BossRewardSequence.IsRunning)
            Right.Open();
    }
}
