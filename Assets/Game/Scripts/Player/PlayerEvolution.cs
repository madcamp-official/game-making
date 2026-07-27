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
        [Tooltip("진화 컷씬에 표시할 정면 스프라이트 (남쪽 대기 1프레임)")]
        public Sprite portrait;
        [Min(1)] public int maxHealth = 100;
        [Min(0)] public int attackDamage = 12;   // 기본 공격 1 (근거리)
        [UnityEngine.Serialization.FormerlySerializedAs("razorDamage")]
        [Min(0)] public int vineDamage = 8;      // 기본 공격 2 (덩굴채찍)
    }

    [SerializeField] private Stage[] stages;
    [SerializeField, Min(0f)] private float flashStepDuration = 0.15f;

    [Tooltip("진화(=보스 클리어) 시 비어 있는 체력 중 몇 할을 채울지. 1이면 완전 회복.")]
    [SerializeField, Range(0f, 1f)] private float healMissingFraction = 0.6f;

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

        // 이미 쓰러진 뒤라면 진화하지 않는다. (플레이어가 죽는 것과 동시에 보스가 죽으면
        // 진화의 완전 회복이 자뭉열매 없이 부활시키는 버그가 있었다.)
        Health health = GetComponent<Health>();
        if (health != null && health.IsDead) return;

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

    /// <summary>
    /// 개발용: 연출 없이 지정 단계로 바로 바꾼다.
    /// <see cref="DevHackPanel"/>에서만 쓰며, 개발이 끝나면 같이 지운다.
    /// </summary>
    public void SetStageImmediate(int index)
    {
        if (stages == null || stages.Length == 0 || IsEvolving) return;
        CurrentStageIndex = Mathf.Clamp(index, 0, stages.Length - 1);
        ApplyStage(stages[CurrentStageIndex]);
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

            Stage previous = stages[CurrentStageIndex - 1];
            Stage next = stages[CurrentStageIndex];

            bool canPlayCutscene = EvolutionCutscene.Instance != null &&
                                   previous.portrait != null && next.portrait != null;
            if (canPlayCutscene)
            {
                // 풀스크린 컷씬. 백색 섬광 순간(onReveal)에 실제 능력치가 바뀐다.
                yield return EvolutionCutscene.Instance.Play(
                    previous.portrait, next.portrait,
                    previous.stageName, next.stageName,
                    () => ApplyStage(next));
            }
            else
            {
                // 컷씬 리소스가 없을 때의 예비 연출: 제자리 흰색 점멸
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage("어라...?! 몸이 빛나기 시작했다!", 1.6f);

                WaitForSeconds step = new WaitForSeconds(flashStepDuration);
                for (int i = 0; i < 6; i++)
                {
                    if (sr != null) sr.color = i % 2 == 0 ? Color.white * 3f : Color.white;
                    yield return step;
                }
                if (sr != null) sr.color = Color.white;

                ApplyStage(next);

                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage(next.stageName + "(으)로 진화했다!", 2.5f);
            }
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

    // 진화 확정: 애니메이터·능력치 교체.
    // 명세(gameplay-spec 6절)는 완전 회복이었으나, 보스 클리어가 너무 후해져서
    // 비어 있는 체력의 일부만 채우도록 바꿨다 (healMissingFraction).
    private void ApplyStage(Stage next)
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null && next.animatorController != null)
            animator.runtimeAnimatorController = next.animatorController;

        // 최대치를 먼저 올린 뒤 회복해야, 늘어난 몫까지 회복 대상에 들어간다.
        // 연출 도중(예비 연출은 게임이 정지되지 않는다) 쓰러졌다면 회복 없이 최대치만 올린다.
        Health health = GetComponent<Health>();
        if (health != null)
        {
            health.SetMaxHealth(next.maxHealth, refill: false);
            if (!health.IsDead) health.HealMissingFraction(healMissingFraction);
        }

        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.SetDamages(next.attackDamage, next.vineDamage);

        // 진화할 때마다 기술을 하나 더 배운다 (처음 둘 → 셋 → 넷).
        PlayerMoves moves = GetComponent<PlayerMoves>();
        if (moves != null) moves.LearnNext();
    }
}
