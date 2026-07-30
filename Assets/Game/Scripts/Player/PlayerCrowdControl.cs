using UnityEngine;

/// <summary>
/// 3층 적들이 플레이어에게 거는 군중 제어(CC)의 공용 창구. 감속·빙결·강제 이동을
/// 한 곳에서 합산해서, 여러 적이 동시에 걸어도 서로 규칙이 어긋나지 않게 한다.
///
/// 이동은 전부 <b>속도</b>로만 민다 — 위치를 직접 쓰면 벽을 뚫는다. 속도는 물리가
/// 벽에서 알아서 막아 준다. 입력도 막지 않는다: 플레이어 속도에 밀리는 속도를
/// 더할 뿐이라, 미는 방향과 반대로 걸으면 그만큼 덜 밀린다.
///
/// <see cref="PlayerController"/>가 FixedUpdate에서 속도를 확정한 뒤에 실행돼야 하므로
/// 실행 순서를 뒤로 미룬다. 갸라도스의 <c>WaterCurrentField</c>가 쓰는 방식과 같다.
/// </summary>
[DefaultExecutionOrder(60)]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCrowdControl : MonoBehaviour
{
    [Tooltip("감속이 아무리 겹쳐도 보장하는 최소 이동 속도 비율.")]
    [SerializeField, Range(0.05f, 1f)] private float minSpeedFraction = 0.3f;
    [Tooltip("넉백 임펄스가 초당 줄어드는 속도. 클수록 짧고 굵게 밀린다.")]
    [SerializeField, Min(1f)] private float impulseDecay = 26f;
    [Tooltip("빙결 중 스프라이트에 섞는 얼음빛.")]
    [SerializeField] private Color freezeTint = new Color(0.55f, 0.8f, 1f, 1f);

    private Rigidbody2D body;
    private PlayerController controller;
    private Health health;
    private SpriteRenderer sprite;

    // 감속 — 가장 강한 것 하나만 적용한다. 겹쳐 곱하면 최소 속도 보장이 무의미해진다.
    private float slowFactor = 1f;
    private float slowUntil = -999f;

    // 빙결
    private float frozenUntil = -999f;
    private float refreezeReadyTime = -999f;
    private Color normalColor = Color.white;
    private bool tinted;

    // 강제 이동 — 지속형(흡인·해류)은 매 FixedUpdate 다시 쌓고, 임펄스(넉백·충격파)는 감쇠한다.
    private Vector2 pendingPush;
    private Vector2 impulse;

    /// <summary>플레이어에서 찾아 오되 없으면 붙여 준다. 능력들이 쓰는 진입점.</summary>
    public static PlayerCrowdControl Of(Component playerPart)
    {
        if (playerPart == null) return null;
        PlayerCrowdControl cc = playerPart.GetComponent<PlayerCrowdControl>();
        return cc != null ? cc : playerPart.gameObject.AddComponent<PlayerCrowdControl>();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) normalColor = sprite.color;
    }

    /// <summary>
    /// 감속을 건다. 이미 더 강한 감속이 걸려 있으면 시간만 잇는다.
    /// <paramref name="factor"/>는 남는 속도 비율 — 0.6이면 40% 감속이다.
    /// </summary>
    public void ApplySlow(float factor, float duration)
    {
        factor = Mathf.Clamp(factor, minSpeedFraction, 1f);
        if (Time.time >= slowUntil || factor <= slowFactor) slowFactor = factor;
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }

    /// <summary>빙결이 가능한 상태인지. 직전 빙결의 면역 시간이 끝나야 참이다.</summary>
    public bool CanFreeze => Time.time >= refreezeReadyTime &&
                             (health == null || !health.IsDead);

    /// <summary>지금 얼어 있는지. 채널 기술이 끝나며 경직을 지울 때 빙결 경직까지 지우지 않기 위해 본다.</summary>
    public bool IsFrozen => Time.time < frozenUntil;

    /// <summary>
    /// 짧게 얼린다. 유일한 하드 CC라 <paramref name="immunity"/> 동안 재빙결을 막는다.
    /// </summary>
    public void Freeze(float duration, float immunity)
    {
        if (!CanFreeze) return;
        frozenUntil = Time.time + duration;
        refreezeReadyTime = frozenUntil + immunity;
        if (controller != null) controller.Stun(duration);
        body.linearVelocity = Vector2.zero;
        if (sprite != null) { sprite.color = freezeTint; tinted = true; }
    }

    /// <summary>
    /// 이번 물리 프레임에 미는 속도를 더한다. 흡인·해류처럼 지속되는 힘은
    /// 매 FixedUpdate 이 함수를 다시 불러야 한다 — 근원이 죽으면 힘도 저절로 끝난다.
    /// </summary>
    public void AddVelocity(Vector2 velocity) => pendingPush += velocity;

    /// <summary>넉백·충격파처럼 한 방에 밀어내는 힘. 감쇠하며 사라진다.</summary>
    public void AddImpulse(Vector2 velocity) => impulse += velocity;

    private void FixedUpdate()
    {
        if (health != null && health.IsDead)
        {
            pendingPush = Vector2.zero;
            impulse = Vector2.zero;
            RestoreTint();
            return;
        }

        Vector2 velocity = body.linearVelocity;

        if (Time.time < slowUntil)
            velocity *= slowFactor;

        // 빙결 중에는 입력이 이미 0(경직)이고, 외부 힘도 얼음에 막힌 셈 치고 버린다.
        if (Time.time < frozenUntil)
        {
            body.linearVelocity = Vector2.zero;
            pendingPush = Vector2.zero;
            impulse = Vector2.zero;
            return;
        }
        RestoreTint();

        velocity += pendingPush;
        pendingPush = Vector2.zero;

        velocity += impulse;
        impulse = Vector2.MoveTowards(impulse, Vector2.zero, impulseDecay * Time.fixedDeltaTime);

        body.linearVelocity = velocity;
    }

    private void RestoreTint()
    {
        if (!tinted || sprite == null) return;
        sprite.color = normalColor;
        tinted = false;
    }
}
