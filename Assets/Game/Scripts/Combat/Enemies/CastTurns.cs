using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 방 안의 적들이 <b>같은 순간에 공격하지 않도록</b> 순번을 나눈다.
///
/// 시작 시점만 흩어 놓는 것으로는 부족하다 (<see cref="EnemyAbility"/>의 initialDelayJitter).
/// 같은 종은 패턴 길이도 쿨다운도 같아서, <b>처음 벌어진 간격이 그대로 굳는다</b> —
/// 모다피 둘이 0.1초 차이로 시작하면 그 판 내내 0.1초 차이로 함께 쏜다. 눈에는 한 번의
/// 공격으로 보이고, 예고를 둘로 읽을 수가 없다.
///
/// 그래서 시전을 시작하기 직전에 여기에 묻는다. 너무 가까우면 <b>시전을 취소하지 않고
/// 뒤로 민다.</b> 밀 때 작은 무작위를 얹는 것이 중요하다 — 막힌 둘을 같은 시각으로 밀면
/// 그때 나란히 풀려서 아무것도 나아지지 않는다.
///
/// <list type="bullet">
/// <item><b>다른 종끼리</b>는 <see cref="DifferentKindGap"/>. 예고 모양이 달라 구분되므로
/// 겹치지만 않으면 된다</item>
/// <item><b>같은 종끼리</b>는 <see cref="SameKindGap"/>. 같은 그림 둘이 겹치면 하나로
/// 읽히므로 확실히 떼어 놓는다</item>
/// </list>
///
/// <b>간격은 자기 쿨다운에 비례해 줄어든다.</b> 고정값만 쓰면 자주 때리는 적이 굶는다 —
/// 캐터피는 쿨다운 1초에 한 방에 셋까지 나오는데, 0.9초씩 떼면 두 마리분(1.8초)이
/// 한 주기(약 1.5초)보다 길어져 <b>공격이 오히려 느려진다.</b> 어렵게 만들려던 것이 쉽게
/// 만드는 셈이다. 그래서 간격에 <see cref="SameKindCooldownShare"/>만큼의 상한을 씌운다.
/// 빠른 공격은 예고도 짧아서(캐터피 0.27초) 0.45초만 벌어져도 둘로 읽힌다.
///
/// ⚠️ <b>같은 종은 한 방에 셋까지다.</b> 상한이 쿨다운의 0.38이라 (마릿수−1) × 0.38 ≤ 1이
/// 성립해야 하고, 그 답이 셋이다. 넷 이상 넣으면 순번이 밀리다 못해 공격 빈도가 크게
/// 떨어진다. 지금 배치의 최대는 1층 1방의 캐터피 셋이다.
///
/// 보스는 여기에 걸리지 않는다 — 전용 컨트롤러가 직접 패턴을 돌리므로 <see cref="EnemyAbility"/>를
/// 거치지 않는다. 보스가 잡몹의 순번을 기다리면 그쪽 패턴이 무너진다.
/// </summary>
public static class CastTurns
{
    /// <summary>종이 다른 적 사이의 최소 간격.</summary>
    private const float DifferentKindGap = 0.35f;

    /// <summary>같은 종 사이의 최소 간격.</summary>
    private const float SameKindGap = 0.9f;

    /// <summary>
    /// 같은 종 간격의 상한 — 자기 쿨다운의 이만큼을 넘지 않는다.
    ///
    /// 캐터피 셋이 붙어 있는 방(1층 1방)으로 값을 잡았다. 0.45면 간격이 0.45초로 벌어지는
    /// 대신 마리당 공격 횟수가 27% 줄고, 0.32면 간격이 0.33초라 아래 다른 종 간격(0.3)과
    /// 거의 같아져 "같은 종은 더 떼어 놓는다"가 무의미해진다. 0.38이 그 사이다 —
    /// 간격 0.38초(다른 종의 1.3배), 횟수 21% 감소.
    /// </summary>
    private const float SameKindCooldownShare = 0.38f;

    /// <summary>다른 종 간격의 상한.</summary>
    private const float DifferentKindCooldownShare = 0.3f;

    /// <summary>뒤로 밀 때 얹는 무작위의 최대치. 막힌 여럿이 나란히 풀리는 것을 막는다.</summary>
    private const float DeferJitter = 0.25f;

    /// <summary>기술 종류마다 마지막으로 시작한 시각.</summary>
    private static readonly Dictionary<Type, float> lastByKind = new Dictionary<Type, float>();

    /// <summary>누구든 마지막으로 시작한 시각.</summary>
    private static float lastAny = float.NegativeInfinity;

    /// <summary>이 기록이 어느 방의 것인지. 방이 바뀌면 통째로 버린다.</summary>
    private static int recordedVisit = -1;

    /// <summary>정적 값이라 판이 바뀌어도 살아남는다. 판마다 빈 상태에서 시작해야 한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        lastByKind.Clear();
        lastAny = float.NegativeInfinity;
        recordedVisit = -1;
    }

    /// <summary>
    /// 지금 시전해도 되는지 묻는다. 되면 그 시각을 기록하고 참을 돌려준다.
    ///
    /// 안 되면 <paramref name="retryAt"/>에 다시 물어볼 시각을 담아 거짓을 돌려준다.
    /// 부르는 쪽은 그 값을 자기 대기 시각으로 삼는다 — 매 프레임 다시 묻지 않게 하려는 것이고,
    /// 무작위를 여기서 한 번만 뽑게 하려는 것이기도 하다.
    /// </summary>
    /// <param name="kind">기술의 형. 같은 종은 같은 기술을 쓰므로 이것이 곧 "같은 종"이다.</param>
    /// <param name="cooldown">
    /// 묻는 쪽의 쿨다운. 간격의 상한을 여기서 뽑는다 — <b>기다리는 쪽</b>의 값을 쓰는 것이
    /// 핵심이다. 늦춰지는 것도, 늦춰져서 손해를 보는 것도 그쪽이다.
    /// </param>
    public static bool TryClaim(Type kind, float cooldown, out float retryAt)
    {
        DropStaleRoom();

        float sameGap = Mathf.Min(SameKindGap, cooldown * SameKindCooldownShare);
        float otherGap = Mathf.Min(DifferentKindGap, cooldown * DifferentKindCooldownShare);

        float now = Time.time;
        float blockedUntil = lastAny + otherGap;
        if (lastByKind.TryGetValue(kind, out float lastSame))
            blockedUntil = Mathf.Max(blockedUntil, lastSame + sameGap);

        if (now < blockedUntil)
        {
            retryAt = blockedUntil + UnityEngine.Random.Range(0f, DeferJitter);
            return false;
        }

        lastAny = now;
        lastByKind[kind] = now;
        retryAt = now;
        return true;
    }

    /// <summary>
    /// 방을 옮겼으면 기록을 버린다. 지난 방의 마지막 시전 때문에 새 방의 첫 공격이
    /// 밀리면, 방에 들어서자마자 아무도 움직이지 않는 순간이 생긴다.
    /// </summary>
    private static void DropStaleRoom()
    {
        if (recordedVisit == CombatRoomController.VisitId) return;
        recordedVisit = CombatRoomController.VisitId;
        lastByKind.Clear();
        lastAny = float.NegativeInfinity;
    }
}
