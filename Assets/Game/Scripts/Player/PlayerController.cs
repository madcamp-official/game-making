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
        if (!ControlEnabled || (health != null && health.IsDead))
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
