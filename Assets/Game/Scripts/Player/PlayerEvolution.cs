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
        StartCoroutine(EvolveRoutine());
    }

    private IEnumerator EvolveRoutine()
    {
        PlayerController controller = GetComponent<PlayerController>();
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (controller != null) controller.ControlEnabled = false;

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

        Health health = GetComponent<Health>();
        if (health != null) health.SetMaxHealth(next.maxHealth, true);

        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.SetAttackDamage(next.attackDamage);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage(next.stageName + "(으)로 진화했다!", 2.5f);

        if (controller != null) controller.ControlEnabled = true;
    }
}
