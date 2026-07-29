using UnityEngine;

/// <summary>
/// 갸라도스가 불러내는 잉어킹. 플레이어를 돕지도 공격하지도 않고, 그 자리에 박혀
/// 이동 경로와 공격 위치만 좁히는 <b>고정 장애물</b>이다.
///
/// * 움직이지 않고, 삼중 해류와 범람의 물리 이동에도 밀리지 않는다 (Kinematic 강체).
/// * 플레이어와 단단하게 충돌하지만 접촉 피해와 공격 능력이 없다.
/// * 근거리·원거리·장판 공격으로 부술 수 있고, 넉백과 경직에는 면역이다.
///
/// 프리팹에 <see cref="EnemyController"/>가 붙어 있는 이유는 하나뿐이다. 플레이어의 공격
/// (<see cref="PlayerCombat"/>, <see cref="MoveZone"/>, <see cref="Projectile"/>)이 "때릴 수 있는 것"을
/// 그 컴포넌트로 찾기 때문에, 이게 없으면 아예 부술 수 없다. 그래서 프리팹에서는 기본 AI를 끄고
/// 골드 보상과 넉백 배율을 0으로 둬서 실제로는 아무 적 행동도 하지 않는다. 런타임에 생성되므로
/// <see cref="CombatRoomController"/>가 방에 들어설 때 세는 적 목록에도 들어가지 않는다 —
/// 잉어킹이 남아 있다고 방 클리어가 막히지 않는다.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EnemyController))]
public class MagikarpObstacle : MonoBehaviour
{
    /// <summary>제자리에서 파닥이는 동작. 남쪽(정면)을 보는 행 하나만 쓴다.</summary>
    private const string HopState = "Hop_0";

    [Tooltip("플레이어를 막는 충돌 반지름. 배치 검사도 이 값을 쓴다.")]
    [SerializeField, Min(0.05f)] private float bodyRadius = 0.55f;

    private Health health;
    /// <summary>파닥이는 동작을 재생하는 Animator. 스프라이트와 같은 자식 오브젝트에 있다.</summary>
    private Animator hopAnimator;

    /// <summary>배치 규칙과 탈출로 검사가 함께 쓰는 반지름.</summary>
    public float BodyRadius => bodyRadius;

    /// <summary>아직 살아서 길을 막고 있는지.</summary>
    public bool IsAlive => health != null && !health.IsDead;

    private void Awake()
    {
        health = GetComponent<Health>();
        hopAnimator = GetComponentInChildren<Animator>();
        // 전용 컨트롤러가 없으므로 추적 AI가 켜져 있으면 그대로 걸어 나간다.
        GetComponent<EnemyController>().SetBasicAIEnabled(false);
    }

    /// <summary>
    /// 파닥이는 박자를 개체마다 어긋나게 한다. 한 번에 서너 마리가 나오는데 모두 같은 순간에
    /// 뛰면 살아 있는 장애물이 아니라 복사해 붙인 장식으로 보인다.
    ///
    /// 몸은 뛰어도 콜라이더는 바닥에 그대로 있다. 막는 자리가 그림을 따라 움직이면
    /// "여기는 못 지나간다"는 규칙 자체가 흔들린다.
    /// </summary>
    private void Start()
    {
        if (hopAnimator == null || hopAnimator.runtimeAnimatorController == null) return;
        hopAnimator.Play(HopState, 0, Random.value);
    }

    /// <summary>페이즈별 체력과 크기를 정한다. 갸라도스가 소환 직후 한 번 호출한다.</summary>
    public void Configure(int maxHealth, float radius)
    {
        bodyRadius = Mathf.Max(0.05f, radius);
        if (health != null) health.SetMaxHealth(Mathf.Max(1, maxHealth));

        // 콜라이더도 같은 반지름으로 맞춘다. 그린 몸과 막는 범위가 다르면 억울하게 걸린다.
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null) circle.radius = bodyRadius;
    }

    /// <summary>
    /// 페이즈 전환·보스 사망, 그리고 똬리치기의 탈출로가 막혔을 때 치운다.
    /// 다음 소환은 이걸 부르지 않는다 — 부수지 않은 잉어킹은 그대로 남아 쌓인다.
    /// </summary>
    public void Remove()
    {
        if (this != null) Destroy(gameObject);
    }
}
