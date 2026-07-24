using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 실시간 8방향 이동. WASD/화살표 입력을 읽어 Rigidbody2D로 이동한다.
/// 마지막으로 바라본 방향을 PlayerCombat 등이 사용할 수 있게 보관한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    public Vector2 FacingDirection { get; private set; } = Vector2.down;
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
        body.linearVelocity = MoveInput * moveSpeed;
    }

    private void HandleDeath()
    {
        ControlEnabled = false;
        body.linearVelocity = Vector2.zero;
    }
}
