using System.Collections;
using UnityEngine;

/// <summary>
/// 일반 전투방을 정리한 뒤의 마무리 순서: <b>스테이지 클리어 글씨 → 기술 강화 선택 → 출구 개방</b>.
///
/// 예전에는 마지막 적이 쓰러지는 프레임에 강화 팔레트가 곧바로 튀어나왔다. 방금 무슨 일이
/// 있었는지 볼 새도 없이 창이 덮으니, 잡은 손맛과 고르는 재미가 서로를 깎아먹었다.
/// 사이에 숨 돌릴 자리를 두는 것이 이 클래스가 하는 전부다.
///
/// <b>고르기 전에는 나갈 수 없다.</b> 통로 구름과 출구 문이 열리는 시점을 여기서 쥔다.
/// 안 그러면 팔레트를 띄워 둔 채로 다음 방에 들어설 수 있고, 그러면 강화가 어느 방의
/// 보상인지가 흐려진다. 보스방의 <see cref="BossRewardSequence"/>와 같은 규칙이다.
///
/// 싸움이 끝났다는 사실(<see cref="CombatRoomController.CombatActive"/>)과 나갈 수 있다는
/// 사실을 <b>갈라 둔 것</b>이 요점이다. 전자는 마지막 적이 죽는 즉시 참이라 씨뿌리기를
/// 뒤늦게 깔 수 없고, 후자는 이 흐름이 끝나야 참이 된다.
/// </summary>
public class RoomClearSequence : MonoBehaviour
{
    /// <summary>글씨가 떠 있는 시간과 지워지는 시간.</summary>
    private const float BannerHold = 0.9f;
    private const float BannerFade = 0.55f;

    /// <summary>글씨가 다 지워지고 팔레트가 뜨기까지의 짧은 사이.</summary>
    private const float BeforePanel = 0.15f;

    /// <summary>
    /// 마무리 순서가 도는 중인지. 참인 동안에는 통로 구름과 출구 문이 닫힌 채로 있다.
    /// 방을 옮기다 코루틴째 사라져도 갇히지 않도록 <see cref="Reset"/>에서 되돌린다.
    /// </summary>
    public static bool IsRunning { get; private set; }

    private static RoomClearSequence instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        IsRunning = false;
        instance = null;
    }

    /// <summary>
    /// 씬에 하나 만들어 둔다. 방이 아니라 여기에 붙는 이유: 방은 다음 방으로 넘어갈 때
    /// 지워지는데, 그때 코루틴이 <see cref="IsRunning"/>을 참으로 둔 채 끊기면 새 방의
    /// 출구가 영영 열리지 않는다.
    /// </summary>
    private static RoomClearSequence Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("RoomClearSequence");
                instance = go.AddComponent<RoomClearSequence>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    /// <summary>
    /// 방 정리 마무리를 시작한다. 출구는 흐름이 끝난 뒤에 <paramref name="exitDoor"/>가 연다.
    /// </summary>
    public static void Begin(ExitDoor exitDoor)
    {
        Instance.StartCoroutine(Instance.Run(exitDoor));
    }

    private IEnumerator Run(ExitDoor exitDoor)
    {
        IsRunning = true;
        try
        {
            // 1. 스테이지 클리어 — 잠깐 뜨고 서서히 사라진다. 게임은 멈추지 않는다.
            StageClearBanner banner = UIManager.Instance != null ? UIManager.Instance.StageClear : null;
            if (banner != null)
            {
                Coroutine showing = banner.Show("스테이지 클리어!", BannerHold, BannerFade);
                if (showing != null) yield return showing;
            }
            else
            {
                yield return new WaitForSecondsRealtime(BannerHold + BannerFade);
            }

            yield return new WaitForSecondsRealtime(BeforePanel);

            // 2. 경험치를 여기서 얹는다. 레벨이 오르면 그 자리에서 강화 팔레트가 뜬다 —
            //    글씨가 다 사라진 뒤라야 둘이 겹치지 않는다.
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddRoomClear();

            // 3. 고를 것이 있으면 고를 때까지 기다린다. 레벨이 오르지 않았으면 곧바로 지난다.
            yield return null;                       // 팔레트가 열릴 한 프레임을 준다
            while (MoveUpgradePanel.IsOpen) yield return null;
        }
        finally
        {
            // 중간에 끊겨도 반드시 연다. 나가지 못하고 갇히는 것이 가장 나쁘다.
            if (exitDoor != null) exitDoor.SetOpen(true);
            IsRunning = false;
        }
    }
}
