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

    /// <summary>
    /// 남은 경직을 지운다. 시전 시간만큼 미리 경직을 걸어 두는 기술(하이드로펌프)이
    /// 도중에 끊겼을 때 쓴다 — 마지막 적이 죽어 시전이 끝났는데 몸만 계속 굳어 있으면,
    /// 승리한 방에서 움직이지 못하는 이상한 순간이 남는다.
    /// </summary>
    public void CancelStun() => stunnedUntil = -999f;

    /// <summary>
    /// 연출이 대신 걸리게 하는 방향. 조작이 꺼진 동안에만 쓴다.
    ///
    /// 방을 옮길 때 주인공이 왼쪽 통로에서 걸어 들어오는 장면에 쓴다. 조작을 끄면
    /// <see cref="MoveInput"/>이 0이 되고 <see cref="FixedUpdate"/>가 속도를 0으로 눌러
    /// 버리므로, 밖에서 Rigidbody를 밀어도 한 프레임 만에 멈춘다. 그래서 <b>같은 통로로</b>
    /// 방향을 넣어 준다 — 그러면 걷는 그림과 발소리, 속도 배율이 평소와 똑같이 나온다.
    /// </summary>
    public void SetScriptedMove(Vector2 direction)
    {
        scriptedMove = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
        if (scriptedMove != Vector2.zero) FacingDirection = scriptedMove;
    }

    /// <summary>연출 이동을 멈춘다.</summary>
    public void ClearScriptedMove() => scriptedMove = Vector2.zero;

    private Vector2 scriptedMove;
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
            // 연출이 걸으라고 넣어 준 방향은 살린다. 죽었으면 그것마저 무시한다.
            bool dead = health != null && health.IsDead;
            MoveInput = dead ? Vector2.zero : scriptedMove;
            if (MoveInput.sqrMagnitude > 0.01f) FacingDirection = MoveInput;
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
