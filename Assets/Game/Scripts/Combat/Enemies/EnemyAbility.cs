using System.Collections;
using UnityEngine;

/// <summary>
/// 잡몹이 쓰는 특수 공격의 공통 뼈대. 쿨다운을 재고, 사거리 안에 플레이어가 있으면
/// <see cref="Perform"/>을 한 번 돌린다.
///
/// 시전 중에는 <see cref="EnemyController"/>의 기본 추적 AI를 꺼 둔다. 둘 다 살아 있으면
/// 같은 Rigidbody의 속도를 서로 덮어써서, 예고하는 동안에도 적이 슬금슬금 움직인다.
/// 보스가 <c>basicAIEnabled</c>를 끄고 직접 이동하는 것과 같은 이유다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyAbility : MonoBehaviour
{
    [Header("발동 조건")]
    [Tooltip("이 거리 안에 플레이어가 있어야 시전한다. 몸 중심 사이 거리다. " +
             "근접기(EnemyMeleeAbility)에서는 굵은 예비 검사일 뿐이고, 실제로 " +
             "휘두를지는 몸 표면 사이 거리(reach)가 정한다.")]
    [SerializeField, Min(0f)] protected float range = 6f;
    [Tooltip("이 거리보다 가까우면 시전하지 않는다. 돌진처럼 달려갈 공간이 있어야 " +
             "성립하는 공격에서, 코앞에서 쓰면 아무 일도 안 일어나는 것을 막는다.")]
    [SerializeField, Min(0f)] private float minRange;
    [SerializeField, Min(0f)] private float cooldown = 3f;
    [Tooltip("방에 들어오자마자 터지지 않도록 첫 시전만 늦춘다.")]
    [SerializeField, Min(0f)] private float initialDelay = 1.2f;
    [Tooltip("첫 시전을 이 시간 안에서 무작위로 더 늦춘다. 같은 종이 여럿 있을 때 " +
             "박자를 흩어 놓는 값이다 — 0이면 전부 같은 순간에 시작한다.")]
    [SerializeField, Min(0f)] private float initialDelayJitter = 0.6f;

    protected EnemyController Controller { get; private set; }
    protected Health Health { get; private set; }
    protected Rigidbody2D Body { get; private set; }
    protected Transform Player { get; private set; }
    protected Health PlayerHealth { get; private set; }

    /// <summary>연출을 붙일 부모. 적이 죽어도 남아야 해서 적이 아니라 방에 붙인다.</summary>
    protected Transform EffectRoot => transform.parent != null ? transform.parent : null;

    protected Vector2 PlayerPosition => Player != null ? (Vector2)Player.position : (Vector2)transform.position;

    protected Vector2 DirectionToPlayer
    {
        get
        {
            Vector2 delta = PlayerPosition - (Vector2)transform.position;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        }
    }

    private EnemyAnimator enemyAnimator;
    private Collider2D ownCollider;
    private Collider2D playerCollider;
    private float nextReadyTime;
    private bool casting;
    /// <summary>같은 적에게 붙은 기술 전부(자기 자신 포함). 서로 겹쳐 나가지 않게 확인한다.</summary>
    private EnemyAbility[] siblings;

    /// <summary>지금 Perform이 도는 중인지. 파생형이 시전 밖 행동(도망 등)과 겹치지 않게 확인한다.</summary>
    protected bool IsCasting => casting;

    /// <summary>
    /// 파생형이 시전 밖에서 몸을 쓰는 동안(닥트리오의 도망) 참으로 둔다.
    /// 그동안 기본 시전을 시작하지 않는다 — 도망치다 말고 되돌아 공격하면 도망이 아니다.
    /// </summary>
    protected bool ExternallyBusy { get; set; }

    protected virtual void Awake()
    {
        Controller = GetComponent<EnemyController>();
        Health = GetComponent<Health>();
        Body = GetComponent<Rigidbody2D>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        siblings = GetComponents<EnemyAbility>();
        ownCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// 두 몸 표면 사이의 거리. 겹쳐 있으면 음수다.
    ///
    /// 중심으로 재지 않는 이유: 중심 거리는 덩치를 그대로 사거리에서 깎아먹는다.
    /// 캐터피는 콜라이더가 1칸이라 몸이 맞닿은 순간에도 중심 거리가 이미 0.8이다.
    /// 표면으로 재면 덩치가 달라도 "코앞"이 늘 같은 값이 된다.
    ///
    /// 시전을 시작할지(<see cref="EnemyMeleeAbility.ReadyToCast"/>)와 맞았는지
    /// (<see cref="PlayerWithinSector"/>)를 <b>같은 자로</b> 재야 한다 — 둘이 어긋나면
    /// 닿지도 않는 거리에서 팔만 뻗는다.
    /// </summary>
    protected float SurfaceDistanceToPlayer()
    {
        Vector2 offset = PlayerPosition - (Vector2)transform.position;

        if (playerCollider == null && PlayerHealth != null)
            playerCollider = PlayerHealth.GetComponent<Collider2D>();
        if (ownCollider != null && playerCollider != null &&
            ownCollider.enabled && playerCollider.enabled)
        {
            ColliderDistance2D gap = ownCollider.Distance(playerCollider);
            if (gap.isValid) return gap.distance;
        }
        return offset.magnitude;
    }

    /// <summary>
    /// 조준 방향 부채꼴 안에, 사거리 안에 플레이어가 있는가. 근접기(캐터피·스라크의 휘두르기,
    /// 성원숭 2연타, 고지 할퀴기)가 타격 프레임마다 이걸로 판정한다.
    ///
    /// 거리는 <see cref="SurfaceDistanceToPlayer"/> — 몸 표면 기준이다.
    /// </summary>
    protected bool PlayerWithinSector(Vector2 aim, float reach, float sweepAngle)
    {
        if (Player == null) return false;
        Vector2 offset = PlayerPosition - (Vector2)transform.position;
        if (Vector2.Angle(aim, offset) > sweepAngle * 0.5f) return false;
        return SurfaceDistanceToPlayer() <= reach;
    }

    /// <summary>
    /// 같은 적의 기술 중 하나라도 시전(또는 그에 준하는 몸놀림) 중인지.
    ///
    /// 기술을 둘 이상 지닌 적이 있다 — 스라크(돌진＋휘두르기), 강챙이(소용돌이＋휘두르기).
    /// 이걸 막지 않으면 두 기술이 같은 Rigidbody를 서로 덮어쓰는 것은 물론이고,
    /// <see cref="Cast"/>가 저장해 둔 <c>hadBasicAI</c>가 어긋나 <b>기본 추적이 영영 꺼진다</b>:
    /// A가 시전을 시작하며 추적을 끄고, 그 사이에 B가 시작하면서 "원래 꺼져 있었다"고 기억한다.
    /// A가 끝나며 켜 주지만 B가 끝나며 다시 꺼 버린다. 되돌릴 주체가 없어 그대로 굳는다.
    /// (스라크가 돌진만 하고 걸어다니지 않던 것이 이 때문이다.)
    /// </summary>
    private bool AnyAbilityBusy()
    {
        if (siblings == null) return casting || ExternallyBusy;
        for (int i = 0; i < siblings.Length; i++)
        {
            EnemyAbility other = siblings[i];
            if (other != null && (other.casting || other.ExternallyBusy)) return true;
        }
        return false;
    }

    /// <summary>
    /// 시전 동작을 재생하며 바라보는 방향을 고정한다.
    /// Charge 시트가 없는 적이면 <see cref="EnemyAnimator"/>가 알아서 무시한다.
    /// </summary>
    protected void PlayAction(string stateName, Vector2 lookDirection)
    {
        if (enemyAnimator != null) enemyAnimator.SetActionState(stateName, lookDirection);
    }

    /// <summary>같은 동작을 처음부터 다시 재생한다 (성원숭 2연타처럼 같은 상태를 연달아 쓸 때).</summary>
    protected void ReplayAction(string stateName, Vector2 lookDirection)
    {
        if (enemyAnimator != null) enemyAnimator.RestartActionState(stateName, lookDirection);
    }

    protected void StopAction()
    {
        if (enemyAnimator != null) enemyAnimator.ClearActionState();
    }

    protected virtual void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            Player = pc.transform;
            PlayerHealth = pc.GetComponent<Health>();
        }
        // 첫 시전만 개체마다 어긋나게 한다.
        //
        // 같은 종이 둘 있으면 방에 들어서는 순간 함께 어그로가 끌리고, 같은 initialDelay와
        // 같은 cooldown으로 도는 탓에 <b>영영 같은 박자로 붙어 다닌다</b> — 둘이 동시에
        // 달려들고 동시에 굳는다. 시작점만 흩어 놓으면 그 뒤로는 패턴 길이가 적중 여부에
        // 따라 달라지므로 저절로 더 벌어진다.
        nextReadyTime = Time.time + initialDelay + Random.Range(0f, initialDelayJitter);
    }

    private void Update()
    {
        if (AnyAbilityBusy() || Health.IsDead || Player == null) return;
        if (PlayerHealth != null && PlayerHealth.IsDead) return;
        if (!Controller.IsAggro) return;
        // 밀려나는 도중에 시전을 시작하면 Cast가 속도를 0으로 눌러 넉백이 한 프레임 만에 끊긴다.
        // 맞으면 밀려나는 것부터 끝내야 때린 보람이 있다.
        if (Controller.IsKnockedBack) return;
        if (Time.time < nextReadyTime) return;

        float distance = Vector2.Distance(transform.position, Player.position);
        if (distance > range || distance < minRange) return;
        if (!ReadyToCast()) return;

        StartCoroutine(Cast());
    }

    /// <summary>
    /// <see cref="range"/> 안에 든 뒤 한 번 더 묻는다. 기본은 항상 참.
    ///
    /// 거리를 다른 방식으로 재는 파생형이 자기 기준으로 시전을 막는 자리다.
    /// <see cref="EnemyMeleeAbility"/>는 중심이 아니라 몸 표면 사이로 재기 때문에,
    /// 여기서 다시 묻지 않으면 닿지도 않는 거리에서 휘두르는 동작만 나온다.
    /// 닥트리오는 "한 마리씩만 잠복 공격"을 여기서 건다.
    /// </summary>
    protected virtual bool ReadyToCast() => true;

    private IEnumerator Cast()
    {
        casting = true;
        // "켜짐"이 아니라 원래 값으로 되돌린다. 닥트리오는 처음부터 꺼져 있다.
        bool hadBasicAI = Controller.BasicAIEnabled;
        Controller.SetBasicAIEnabled(false);
        Body.linearVelocity = Vector2.zero;

        yield return Perform();

        // Perform이 중간에 빠져나가도 동작이 얼어붙은 채 남지 않게 여기서 반드시 되돌린다.
        StopAction();
        // 시전 도중에 죽었으면 되돌릴 게 없다. HandleDeath가 이미 정리했다.
        if (!Health.IsDead) Controller.SetBasicAIEnabled(hadBasicAI);
        nextReadyTime = Time.time + cooldown;
        casting = false;
    }

    /// <summary>실제 공격. 예고와 발동을 여기서 전부 처리한다.</summary>
    protected abstract IEnumerator Perform();
}
