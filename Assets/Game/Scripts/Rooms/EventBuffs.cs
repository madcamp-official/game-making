using UnityEngine;

/// <summary>
/// 이벤트에서 얻은, 판이 끝날 때까지 유지되는 강화. 유물과 달리 아이콘도 없고 되돌릴 수도 없다.
///
/// <see cref="RelicManager"/>에 얹지 않고 따로 둔 이유: 그쪽은 "유물 목록에서 배율을 다시 계산"하는
/// 구조라, 유물이 아닌 값을 넣으면 유물 하나 얻을 때마다 같이 날아간다.
///
/// 씬에 미리 놓지 않고 처음 필요할 때 스스로 만든다. 방을 옮겨도 유지된다.
/// </summary>
public class EventBuffs : MonoBehaviour
{
    private static EventBuffs instance;

    /// <summary>없으면 만들어서 돌려준다. 이벤트가 강화를 줄 때만 생긴다.</summary>
    public static EventBuffs Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("EventBuffs");
                instance = go.AddComponent<EventBuffs>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    /// <summary>아직 하나도 안 생겼으면 null. 소비하는 쪽은 이걸 써서 괜히 만들지 않는다.</summary>
    public static EventBuffs Existing => instance;

    public float MeleeDamageMultiplier { get; private set; } = 1f;
    public float RangedDamageMultiplier { get; private set; } = 1f;
    public float MoveSpeedMultiplier { get; private set; } = 1f;

    /// <summary>판이 새로 시작되면 초기화한다. 정적 값이라 두지 않으면 다음 판까지 따라간다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => instance = null;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    public void AddMeleeDamage(float fraction) => MeleeDamageMultiplier *= 1f + fraction;
    public void AddRangedDamage(float fraction) => RangedDamageMultiplier *= 1f + fraction;

    public void AddMoveSpeed(float fraction)
    {
        MoveSpeedMultiplier *= 1f + fraction;
        // 이동 속도는 PlayerRelicEffects가 유물 목록이 바뀔 때만 다시 밀어 넣는다.
        // 이벤트로 올린 값은 그 신호를 타지 않으므로 여기서 직접 반영한다.
        PlayerRelicEffects effects = FindAnyObjectByType<PlayerRelicEffects>();
        if (effects != null) effects.RefreshSpeed();
    }

    // --- 소비하는 쪽이 쓰는 정적 접근자. 아직 강화가 없으면 1을 돌려준다. ---
    public static float Melee => instance != null ? instance.MeleeDamageMultiplier : 1f;
    public static float Ranged => instance != null ? instance.RangedDamageMultiplier : 1f;
    public static float MoveSpeed => instance != null ? instance.MoveSpeedMultiplier : 1f;
}
