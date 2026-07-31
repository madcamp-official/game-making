using System.Collections;
using UnityEngine;

/// <summary>
/// 잡몹의 단거리 공격. 코앞의 플레이어를 향해 한 번 휘두른다.
///
/// 예전에는 적이 몸에 닿기만 하면 자동으로 피해를 줬다. 피할 방법도, 피했다는 감각도 없이
/// 붙은 채로 체력이 깎이기만 해서 근접전이 밀치기 싸움이 됐다. 이제 접촉 피해는 없고,
/// 때리는 적만 이 컴포넌트로 <b>보이는 동작</b>과 함께 때린다 — 휘두르는 그림이 뜨고,
/// 그 순간 앞쪽 부채꼴에 있어야 맞는다. 뒤로 물러나거나 옆으로 돌면 빗나간다.
///
/// 휘두르기는 <b>이미 닿는 거리일 때만</b> 시작한다 (<see cref="ReadyToCast"/>). 사거리는
/// 몸통박치기가 닿는 거리보다 짧게 잡는다 — 적이 더 멀리서 때리면 근접전에서 플레이어가
/// 먼저 손을 댈 방법이 없어진다.
///
/// 조준은 동작이 시작될 때 고정한다. 타격 순간까지 따라오면 걸어서 피할 수가 없다.
/// 타격 시점(<see cref="hitDelay"/>)은 SpriteCollab AnimData의 HitFrame에 맞춰 둔다 —
/// 그림에서 실제로 때리는 프레임과 판정이 어긋나면 맞아도 억울하고 피해도 억울하다.
///
/// 시작 조건과 타격 판정은 <see cref="EnemyAbility.SurfaceDistanceToPlayer"/>라는
/// 같은 자로 잰다 — 몸 표면 기준이라 덩치가 사거리를 깎아먹지 않는다.
/// </summary>
public class EnemyMeleeAbility : EnemyAbility
{
    [Header("휘두르기")]
    [Tooltip("이 동작 이름으로 애니메이터 상태를 재생한다 (Attack·Slice 등).")]
    [SerializeField] private string actionState = "Attack";
    [Tooltip("몸 표면에서 얼마나 더 뻗는지. 중심이 아니라 표면 기준이라, 덩치가 커도 " +
             "체감 사거리가 줄지 않는다. 휘두르기 시작할지도 이 값으로 정한다. " +
             "몸통박치기가 닿는 거리(표면 기준 1.45)보다 반드시 짧아야 한다 — " +
             "적이 더 멀리서 때리면 근접전에서 먼저 손을 댈 방법이 없어진다.")]
    [SerializeField, Min(0.1f)] private float reach = 1f;
    [Tooltip("판정 부채꼴의 전체 각도(도). 넓을수록 옆으로 돌아 피하기 어렵다.")]
    [SerializeField, Range(20f, 360f)] private float sweepAngle = 120f;
    [Tooltip("동작이 시작되고 실제로 맞기까지의 시간. 원본 시트의 HitFrame에 맞춘다.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.2f;
    [Tooltip("동작을 끝까지 보여 주고 다음 행동으로 넘어가기까지의 시간.")]
    [SerializeField, Min(0f)] private float recovery = 0.35f;
    [SerializeField, Min(0)] private int damage = 8;
    [Tooltip("켜면 방에 적이 저 하나 남았을 때만 휘두른다. 원거리가 주무기인 적(쥬레곤)이 " +
             "무리 속에서까지 근접기를 섞으면 붙는 것 자체가 벌칙이 되어서, 마지막 한 마리의 " +
             "발악으로만 남긴다.")]
    [SerializeField] private bool onlyWhenLastEnemy;
    // 성원숭 2연타의 예고와 같은 위험색. "여기 서 있으면 맞는다" 하나만 뜻한다.
    [Tooltip("피해 부채꼴. 그린 그대로가 맞는 자리다.")]
    [SerializeField] private Color telegraphColor = new Color(0.88f, 0.12f, 0.2f, 0.45f);

    /// <summary>내가 속한 전투방. 혼자 남았는지는 방의 생존 수로 판단한다.</summary>
    private CombatRoomController room;
    /// <summary>예고 부채꼴의 반지름 계산에 쓰는 내 콜라이더.</summary>
    private Collider2D bodyCollider;

    protected override void Awake()
    {
        base.Awake();
        room = GetComponentInParent<CombatRoomController>();
        bodyCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// 예고 부채꼴의 반지름. 판정(<see cref="EnemyAbility.PlayerWithinSector"/>)은 몸 표면
    /// 사이 거리 ≤ reach인데 그림은 몸 중심에서 그리므로, 내 몸 반지름과 플레이어 반지름을
    /// 더해 <b>넘칠지언정 모자라지 않게</b> 부풀린다 — 예고 밖에 서 있다가 맞으면 안 된다.
    /// </summary>
    private float TelegraphRadius()
    {
        float own = bodyCollider != null
            ? Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) : 0.4f;
        Collider2D playerCol = PlayerHealth != null ? PlayerHealth.GetComponent<Collider2D>() : null;
        float player = playerCol != null
            ? Mathf.Max(playerCol.bounds.extents.x, playerCol.bounds.extents.y) : 0.3f;
        return own + reach + player;
    }

    protected override IEnumerator Perform()
    {
        Vector2 aim = DirectionToPlayer;
        PlayAction(actionState, aim);

        // 예고 — 재는 것과 같은 부채꼴을 타격 순간까지 그려 둔다. 예고 시간이 곧 hitDelay라
        // 따로 시간을 벌지 않고, 공격 박자도 그대로다.
        AttackTelegraph zone = AttackTelegraph.CreateSector(
            EffectRoot, transform.position, TelegraphRadius(),
            Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, sweepAngle, telegraphColor);
        zone.Hold(hitDelay);

        float hitAt = Time.time + hitDelay;
        while (Time.time < hitAt && !Health.IsDead)
        {
            // 예고 중 넉백으로 밀리면 판정 기준(내 몸)도 함께 움직인다. 그림만 처음
            // 자리에 남으면 예고가 거짓말이 되므로 몸에 붙여 둔다.
            if (zone != null) zone.transform.position = transform.position;
            yield return null;
        }
        if (Health.IsDead) yield break;

        if (PlayerWithinSector(aim, reach, sweepAngle) && PlayerHealth != null && !PlayerHealth.IsDead)
            PlayerHealth.TakeDamage(damage);

        // 휘두른 자세를 끝까지 보여 준다. 그동안 제자리에 선다.
        float end = Time.time + recovery;
        while (Time.time < end && !Health.IsDead)
        {
            HoldPosition();
            yield return null;
        }
        StopAction();
    }

    /// <summary>
    /// <b>지금 닿는 거리일 때만</b> 휘두르기 시작한다.
    ///
    /// 부모의 <c>range</c>는 몸 중심 사이로 재는 굵은 예비 검사다. 여기서 쓰는 사거리는
    /// 몸 표면 사이로 재기 때문에 재는 방식도 크기도 다르다 — 캐터피는 중심 사이 2.6에서
    /// 시전을 시작하는데 표면 사거리 1.4는 중심 사이로 치면 2.2다. 아직 0.4칸이나 모자란
    /// 자리에서 이미 팔을 휘두르고 있었고, 휘두르는 0.27초 동안 플레이어는 1.35칸을
    /// 더 벌린다. <b>안 맞는 거리에서 때리는 시늉만 하던 것이 이 때문이다.</b>
    ///
    /// 시작할 때 닿는 거리였어도 타격 순간에 벗어나 있으면 빗나가는 것은 그대로다.
    /// 물러나서 피하는 손맛은 남기고, 애초에 닿을 리 없는 헛손질만 없앤다.
    /// </summary>
    protected override bool ReadyToCast()
    {
        // 방에 다른 적이 남아 있으면 휘두르지 않는다. 방 밖(개발용 배치)이면 조건이 없다.
        if (onlyWhenLastEnemy && room != null && room.AliveEnemyCount > 1) return false;
        return SurfaceDistanceToPlayer() <= reach;
    }
}
