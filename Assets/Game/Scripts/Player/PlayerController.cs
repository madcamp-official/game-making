using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 실시간 8방향 이동. WASD/화살표 입력을 읽어 Rigidbody2D로 이동한다.
/// 마지막으로 바라본 방향을 PlayerCombat 등이 사용할 수 있게 보관한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    /// <summary>공격 중 감속 등 외부 요인이 곱해지는 이동 속도 배율.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>유물로 인한 이동 속도 배율 (구애스카프). 공격 감속과 따로 곱해진다.</summary>
    public float RelicSpeedMultiplier { get; set; } = 1f;

    /// <summary>공격 등 외부 요인으로 바라보는 방향을 바꾼다.</summary>
    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            FacingDirection = direction.normalized;
    }
    public Vector2 MoveInput { get; private set; }
    public bool IsMoving => MoveInput.sqrMagnitude > 0.01f;
    public bool ControlEnabled { get; set; } = true;

    /// <summary>덩굴채찍 후딜 등으로 잠시 움직일 수 없는 상태.</summary>
    public bool IsStunned => Time.time < stunnedUntil;

    /// <summary>
    /// <paramref name="duration"/>초 동안 이동을 막는다. 이미 걸린 경직이 더 길면 그대로 둔다.
    /// <see cref="ControlEnabled"/>와 따로 두는 이유: 그쪽은 게임 오버·컷씬이 켜고 끄는 값이라,
    /// 짧은 경직이 끝나며 되돌려 놓으면 꺼져 있어야 할 조작이 되살아난다.
    /// </summary>
    public void Stun(float duration)
    {
        if (duration <= 0f) return;
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
    }

    private float stunnedUntil = -999f;
    private Rigidbody2D body;
    private Health health;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        if (health != null) health.OnDied += HandleDeath;
    }

    private void Update()
    {
        // 경직 중에는 입력을 아예 읽지 않는다. 바라보는 방향은 그대로 둬서,
        // 채찍을 휘두른 자세 그대로 굳었다가 풀리게 한다.
        if (!ControlEnabled || IsStunned || (health != null && health.IsDead))
        {
            MoveInput = Vector2.zero;
            return;
        }

        Vector2 input = Vector2.zero;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
        }

        MoveInput = input.normalized;
        if (MoveInput.sqrMagnitude > 0.01f)
            FacingDirection = MoveInput;
    }

    private void FixedUpdate()
    {
        body.linearVelocity = MoveInput * (moveSpeed * SpeedMultiplier * RelicSpeedMultiplier);
    }

    private void HandleDeath()
    {
        ControlEnabled = false;
        body.linearVelocity = Vector2.zero;
    }
}
