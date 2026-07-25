using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 처치 후 진화 처리: 애니메이터 교체와 능력치 상승.
/// </summary>
public class PlayerEvolution : MonoBehaviour
{
    [System.Serializable]
    public class Stage
    {
        public string stageName;
        public RuntimeAnimatorController animatorController;
        [Min(1)] public int maxHealth = 100;
        [Min(0)] public int attackDamage = 12;   // 기본 공격 1 (근거리)
        [Min(0)] public int razorDamage = 8;     // 기본 공격 2 (잎날가르기)
    }

    [SerializeField] private Stage[] stages;
    [SerializeField, Min(0f)] private float flashStepDuration = 0.15f;

    public int CurrentStageIndex { get; private set; }

    /// <summary>진화 연출이 진행 중인지. 연출 중 재진입을 막는다.</summary>
    public bool IsEvolving { get; private set; }

    public void Evolve()
    {
        // 연출 중 재진입 차단. 보스방 클리어는 "보상 유물 지급 → 진화" 순서로 일어나는데,
        // 보상 유물이 행복의알이면 유물 효과가 먼저 Evolve()를 호출한다.
        // 예전에는 이때 두 번째 연출이 겹치면서 Kinematic 상태를 원래 상태로 잘못 기억해
        // 연출이 끝난 뒤에도 Kinematic으로 남았고(=벽을 통과), 단계도 한 번에 두 칸 올라갔다.
        if (IsEvolving) return;

        if (stages == null || CurrentStageIndex + 1 >= stages.Length) return;

        // 층당 최대 1단계: N층에서는 N단계까지만 진화할 수 있다.
        // (행복의알로 조기 진화했다면 같은 층 보스 처치로 또 진화하지 않는다.)
        if (RoomFlowController.Instance != null &&
            CurrentStageIndex + 1 > RoomFlowController.Instance.CurrentFloorIndex + 1)
            return;

        IsEvolving = true;
        CurrentStageIndex++; // 연출 시작과 동시에 단계를 확정한다 (중복 진화 방지)
        StartCoroutine(EvolveRoutine());
    }

    private IEnumerator EvolveRoutine()
    {
        PlayerController controller = GetComponent<PlayerController>();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Rigidbody2D body = GetComponent<Rigidbody2D>();

        try
        {
            if (controller != null) controller.ControlEnabled = false;

            // 진화 연출 중에는 물리적으로 밀리지 않도록 고정한다 (벽 뚫림 방지).
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("어라...?! 몸이 빛나기 시작했다!", 1.6f);

            // 흰색 점멸 연출
            WaitForSeconds step = new WaitForSeconds(flashStepDuration);
            for (int i = 0; i < 6; i++)
            {
                if (sr != null) sr.color = i % 2 == 0 ? Color.white * 3f : Color.white;
                yield return step;
            }
            if (sr != null) sr.color = Color.white;

            Stage next = stages[CurrentStageIndex];

            Animator animator = GetComponent<Animator>();
            if (animator != null && next.animatorController != null)
                animator.runtimeAnimatorController = next.animatorController;

            // 명세(gameplay-spec 6절): 진화 시 최대 체력 증가 + 체력 완전 회복
            Health health = GetComponent<Health>();
            if (health != null) health.SetMaxHealth(next.maxHealth, true);

            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.SetDamages(next.attackDamage, next.razorDamage);

            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage(next.stageName + "(으)로 진화했다!", 2.5f);
        }
        finally
        {
            // 연출이 중간에 끊겨도(코루틴 정지, 오브젝트 비활성화) 반드시 원상 복구한다.
            // 되돌릴 상태를 기억하지 않고 항상 Dynamic으로 되돌리는 것이 핵심이다.
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Dynamic;
            }
            if (sr != null) sr.color = Color.white;
            if (controller != null) controller.ControlEnabled = true;
            IsEvolving = false;
        }
    }
}
