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
    [Tooltip("이 거리 안에 플레이어가 있어야 시전한다.")]
    [SerializeField, Min(0f)] protected float range = 6f;
    [Tooltip("이 거리보다 가까우면 시전하지 않는다. 돌진처럼 달려갈 공간이 있어야 " +
             "성립하는 공격에서, 코앞에서 쓰면 아무 일도 안 일어나는 것을 막는다.")]
    [SerializeField, Min(0f)] private float minRange;
    [SerializeField, Min(0f)] private float cooldown = 3f;
    [Tooltip("방에 들어오자마자 터지지 않도록 첫 시전만 늦춘다.")]
    [SerializeField, Min(0f)] private float initialDelay = 1.2f;

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
    private float nextReadyTime;
    private bool casting;

    protected virtual void Awake()
    {
        Controller = GetComponent<EnemyController>();
        Health = GetComponent<Health>();
        Body = GetComponent<Rigidbody2D>();
        enemyAnimator = GetComponent<EnemyAnimator>();
    }

    /// <summary>
    /// 시전 동작을 재생하며 바라보는 방향을 고정한다.
    /// Charge 시트가 없는 적이면 <see cref="EnemyAnimator"/>가 알아서 무시한다.
    /// <paramref name="normalizedTime"/>은 재생을 시작할 지점 (1이면 마지막 프레임에서 굳는다).
    /// </summary>
    protected void PlayAction(string stateName, Vector2 lookDirection, float normalizedTime = -1f)
    {
        if (enemyAnimator != null)
            enemyAnimator.SetActionState(stateName, lookDirection, normalizedTime);
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
        nextReadyTime = Time.time + initialDelay;
    }

    private void Update()
    {
        if (casting || Health.IsDead || Player == null) return;
        if (PlayerHealth != null && PlayerHealth.IsDead) return;
        if (!Controller.IsAggro) return;
        if (Time.time < nextReadyTime) return;

        float distance = Vector2.Distance(transform.position, Player.position);
        if (distance > range || distance < minRange) return;

        StartCoroutine(Cast());
    }

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
