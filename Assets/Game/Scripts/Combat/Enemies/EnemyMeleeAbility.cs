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
/// 조준은 동작이 시작될 때 고정한다. 타격 순간까지 따라오면 걸어서 피할 수가 없다.
/// 타격 시점(<see cref="hitDelay"/>)은 SpriteCollab AnimData의 HitFrame에 맞춰 둔다 —
/// 그림에서 실제로 때리는 프레임과 판정이 어긋나면 맞아도 억울하고 피해도 억울하다.
///
/// 거리 판정은 <see cref="EnemyAbility.PlayerWithinSector"/> — 몸 표면 기준이라
/// 덩치가 사거리를 깎아먹지 않는다.
/// </summary>
public class EnemyMeleeAbility : EnemyAbility
{
    [Header("휘두르기")]
    [Tooltip("이 동작 이름으로 애니메이터 상태를 재생한다 (Attack·Slice 등).")]
    [SerializeField] private string actionState = "Attack";
    [Tooltip("몸 표면에서 얼마나 더 뻗는지. 중심이 아니라 표면 기준이라, 덩치가 커도 " +
             "체감 사거리가 줄지 않는다. 대략 (플레이어 속도 x hitDelay)만큼은 줘야 " +
             "휘두르는 동안 걸어 나간 플레이어에게 닿는다.")]
    [SerializeField, Min(0.1f)] private float reach = 1.3f;
    [Tooltip("판정 부채꼴의 전체 각도(도). 넓을수록 옆으로 돌아 피하기 어렵다.")]
    [SerializeField, Range(20f, 360f)] private float sweepAngle = 120f;
    [Tooltip("동작이 시작되고 실제로 맞기까지의 시간. 원본 시트의 HitFrame에 맞춘다.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.2f;
    [Tooltip("동작을 끝까지 보여 주고 다음 행동으로 넘어가기까지의 시간.")]
    [SerializeField, Min(0f)] private float recovery = 0.35f;
    [SerializeField, Min(0)] private int damage = 8;

    protected override IEnumerator Perform()
    {
        Vector2 aim = DirectionToPlayer;
        PlayAction(actionState, aim);

        yield return new WaitForSeconds(hitDelay);
        if (Health.IsDead) yield break;

        if (PlayerWithinSector(aim, reach, sweepAngle) && PlayerHealth != null && !PlayerHealth.IsDead)
            PlayerHealth.TakeDamage(damage);

        // 휘두른 자세를 끝까지 보여 준다. 그동안 제자리에 선다.
        float end = Time.time + recovery;
        while (Time.time < end && !Health.IsDead)
        {
            Body.linearVelocity = Vector2.zero;
            yield return null;
        }
        StopAction();
    }
}
