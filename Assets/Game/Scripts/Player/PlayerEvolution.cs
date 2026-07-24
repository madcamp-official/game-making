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
        public int maxHealth = 10;
        public int attackDamage = 3;
    }

    [SerializeField] private Stage[] stages;

    public int CurrentStageIndex { get; private set; }

    public void Evolve()
    {
        if (stages == null || CurrentStageIndex + 1 >= stages.Length) return;

        // 층당 최대 1단계: N층에서는 N단계까지만 진화할 수 있다.
        // (행복의알로 조기 진화했다면 같은 층 보스 처치로 또 진화하지 않는다.)
        if (RoomFlowController.Instance != null &&
            CurrentStageIndex + 1 > RoomFlowController.Instance.CurrentFloorIndex + 1)
            return;

        StartCoroutine(EvolveRoutine());
    }

    private IEnumerator EvolveRoutine()
    {
        PlayerController controller = GetComponent<PlayerController>();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (controller != null) controller.ControlEnabled = false;

        // 진화 연출 중에는 물리적으로 밀리지 않도록 고정한다 (벽 뚫림 방지).
        RigidbodyType2D prevBodyType = RigidbodyType2D.Dynamic;
        if (body != null)
        {
            prevBodyType = body.bodyType;
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage("어라...?! 몸이 빛나기 시작했다!", 1.6f);

        // 흰색 점멸 연출
        for (int i = 0; i < 6; i++)
        {
            if (sr != null) sr.color = i % 2 == 0 ? Color.white * 3f : Color.white;
            yield return new WaitForSeconds(0.15f);
        }
        if (sr != null) sr.color = Color.white;

        CurrentStageIndex++;
        Stage next = stages[CurrentStageIndex];

        Animator animator = GetComponent<Animator>();
        if (animator != null && next.animatorController != null)
            animator.runtimeAnimatorController = next.animatorController;

        // 최대 체력만 늘리고 현재 체력은 유지한다 (회복은 층 이동 시).
        Health health = GetComponent<Health>();
        if (health != null) health.SetMaxHealth(next.maxHealth, false);

        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.SetAttackDamage(next.attackDamage);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage(next.stageName + "(으)로 진화했다!", 2.5f);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.bodyType = prevBodyType;
        }
        if (controller != null) controller.ControlEnabled = true;
    }
}
