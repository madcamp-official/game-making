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

        [Tooltip("진화 컷씬 전용 큰 그림 (Art/Characters/Evolution). 비우면 portrait을 쓴다.")]
        public Sprite evolutionArt;

        [Tooltip("결과 화면 — 쓰러졌을 때 띄울 Dizzy 표정 초상.")]
        public Sprite dizzyPortrait;

        [Tooltip("결과 화면 — 클리어했을 때 띄울 Happy 표정 초상.")]
        public Sprite happyPortrait;

        [Min(1)] public int maxHealth = 100;

        /// <summary>컷씬에 세울 그림. 전용 그림이 없으면 예전처럼 정면 스프라이트로 버틴다.</summary>
        public Sprite CutsceneArt => evolutionArt != null ? evolutionArt : portrait;

        [Tooltip("현재 기술 세트의 슬롯 순서대로 적는 단계별 기준 위력. 0이면 기술 구현의 기본값을 쓴다.")]
        public int[] movePowers;

        // 기존 이상해씨 에셋을 안전하게 읽기 위한 이전 필드. movePowers가 채워진 뒤에도
        // 구버전 에셋·프리팹을 열 수 있게 당분간 남겨 둔다.
        [HideInInspector, Min(0)] public int attackDamage = 11;   // 구버전 기본 공격 1
        [UnityEngine.Serialization.FormerlySerializedAs("razorDamage")]
        [HideInInspector, Min(0)] public int vineDamage = 4;      // 구버전 기본 공격 2
    }

    [SerializeField] private Stage[] stages;
    [SerializeField, Min(0f)] private float flashStepDuration = 0.15f;

    [Tooltip("진화(=보스 클리어) 시 비어 있는 체력 중 몇 할을 채울지. 1이면 완전 회복. " +
             "최대 체력을 올린 뒤에 채우므로 늘어난 몫까지 회복 대상에 들어간다 — " +
             "겉보기 회복량은 이 값보다 크다.")]
    [SerializeField, Range(0f, 1f)] private float healMissingFraction = 0.45f;

    public int CurrentStageIndex { get; private set; }

    /// <summary>
    /// 이 층에 들어온 뒤 진화로 체력을 채운 적이 있는지.
    ///
    /// 층을 넘어갈 때도 회복이 있어서(<see cref="RoomFlowController"/>), 보스를 잡고 나가면
    /// 한 층에 회복이 두 번 겹쳤다. 각각 비어 있는 체력의 6할이면 합쳐서 8할 4푼이 찬다 —
    /// 체력 관리라는 것이 사실상 사라진다. 층마다 <b>한 번만</b> 회복하도록, 이미 진화로
    /// 채웠으면 층 회복을 건너뛴다.
    ///
    /// 행복의알로 상점에서 미리 진화했을 때도 같은 값이 서므로, 진화 시점이 앞당겨져도
    /// "층당 한 번"은 그대로다.
    /// </summary>
    public bool HealedThisFloor { get; private set; }

    /// <summary>
    /// 이 층에 들어온 뒤 실제로 진화한 적이 있는지.
    ///
    /// 진화는 <b>층당 한 번</b>이다. 아래 CanEvolve의 "N층에서는 N단계까지" 제한만으로는
    /// 모자랐다 — 그 제한은 단계가 층을 <b>앞서는</b> 것만 막아서, 단계가 층보다 뒤처진
    /// 판(진화를 B로 그만두었거나 늦게 시작한 판)에서는 행복의알로 상점에서 한 번,
    /// 같은 층 보스를 잡고 또 한 번 — 두 번이 통과됐다. 행복의알이 주는 것은 순서이지
    /// 횟수가 아니다.
    ///
    /// 취소한 진화는 세지 않는다 — 이 값은 능력치가 실제로 바뀌는 자리(ApplyStage)에서만
    /// 선다.
    /// </summary>
    public bool EvolvedThisFloor { get; private set; }

    /// <summary>층이 바뀌었다. 다음 층의 회복 한 번과 진화 한 번을 되살린다.</summary>
    public void NotifyFloorChanged()
    {
        HealedThisFloor = false;
        EvolvedThisFloor = false;
    }

    /// <summary>진화 연출이 진행 중인지. 연출 중 재진입을 막는다.</summary>
    public bool IsEvolving { get; private set; }

    /// <summary>가장 최근 진화에서 새로 배운 기술. 배울 것이 없었으면 null.</summary>
    public MoveType? LastLearnedMove { get; private set; }

    /// <summary>
    /// 지금 <see cref="Evolve"/>를 부르면 실제로 진화하는지.
    ///
    /// 보스 보상 흐름이 진화·기술 습득 단계를 통째로 건너뛸지 <b>미리</b> 정해야 해서 따로 뺐다.
    /// 불러 보고 알아내는 방법은 쓸 수 없다 — 진화는 부르는 순간 시작되기 때문이다.
    /// </summary>
    public bool CanEvolve
    {
        get
        {
            // 연출 중 재진입 차단. 진화를 부르는 곳이 둘이다 — 보스방 클리어와, 행복의알을 지닌 채
            // 상점방을 나갈 때(RoomFlowController.TryHappyEggEvolve). 아래 "층당 한 단계" 제한이
            // 둘을 갈라 주지만, 연출이 겹치는 것은 그 전에 여기서 막는다.
            // 예전에는 두 번째 연출이 겹치면서 Kinematic 상태를 원래 상태로 잘못 기억해
            // 연출이 끝난 뒤에도 Kinematic으로 남았고(=벽을 통과), 단계도 한 번에 두 칸 올라갔다.
            if (IsEvolving) return false;

            // 이미 쓰러진 뒤라면 진화하지 않는다. (플레이어가 죽는 것과 동시에 보스가 죽으면
            // 진화의 완전 회복이 기력의 덩어리 없이 부활시키는 버그가 있었다.)
            Health health = GetComponent<Health>();
            if (health != null && health.IsDead) return false;

            if (stages == null || CurrentStageIndex + 1 >= stages.Length) return false;

            // 층당 한 번. 행복의알로 상점에서 미리 진화했다면 같은 층 보스로 또 진화하지
            // 않는다 — 아래 단계 제한은 뒤처진 판에서 이 경우를 놓친다 (EvolvedThisFloor 참고).
            if (EvolvedThisFloor) return false;

            // 층당 최대 1단계: N층에서는 N단계까지만 진화할 수 있다.
            // 단계가 층을 앞서지 못하게 하는 상한이다.
            if (RoomFlowController.Instance != null &&
                CurrentStageIndex + 1 > RoomFlowController.Instance.CurrentFloorIndex + 1)
                return false;

            return true;
        }
    }

    public void Evolve()
    {
        if (!CanEvolve) return;

        IsEvolving = true;
        CurrentStageIndex++; // 연출 시작과 동시에 단계를 확정한다 (중복 진화 방지)
        StartCoroutine(EvolveRoutine());
    }

    /// <summary>
    /// 고른 캐릭터의 진화 단계를 통째로 갈아 끼우고 1단계로 되돌린다.
    ///
    /// 캐릭터가 바뀌면 그림·체력·공격력·진화 뒤 모습이 전부 달라진다. 하나씩 옮기는 대신
    /// 배열째 바꾸면 <see cref="ApplyStage"/>가 나머지를 알아서 맞춘다 — 항목이 늘어나도
    /// 여기를 고칠 일이 없다. <see cref="GameFlow"/>가 판을 시작할 때 부른다.
    /// </summary>
    public void LoadStages(Stage[] newStages)
    {
        if (newStages == null || newStages.Length == 0) return;
        stages = newStages;
        LastLearnedMove = null;
        HealedThisFloor = false;
        EvolvedThisFloor = false;
        SetStageImmediate(0);
    }

    /// <summary>
    /// 개발용: 연출 없이 지정 단계로 바로 바꾼다.
    /// <see cref="DevHackPanel"/>에서만 쓰며, 개발이 끝나면 같이 지운다.
    /// 판을 시작할 때 1단계를 입히는 데에도 쓴다 (<see cref="LoadStages"/>).
    /// </summary>
    public void SetStageImmediate(int index)
    {
        if (stages == null || stages.Length == 0 || IsEvolving) return;
        CurrentStageIndex = Mathf.Clamp(index, 0, stages.Length - 1);
        // 기술은 배우지 않는다. 이건 "단계를 입힌다"이지 "진화한다"가 아니다.
        ApplyStage(stages[CurrentStageIndex], learnMove: false);
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
                                   previous.CutsceneArt != null && next.CutsceneArt != null;
            if (canPlayCutscene)
            {
                // 풀스크린 컷씬. 백색 섬광 순간(onReveal)에 실제 능력치가 바뀐다.
                yield return EvolutionCutscene.Instance.Play(
                    previous.CutsceneArt, next.CutsceneArt,
                    previous.stageName, next.stageName,
                    () => ApplyStage(next, learnMove: true));

                // B로 그만두었다면 올려 둔 단계를 도로 내린다.
                //
                // 단계는 <see cref="Evolve"/>가 <b>연출을 시작하기 전에</b> 올린다 — 연출이 도는
                // 동안 진화가 두 번 걸리는 것을 막으려는 것이다. 그래서 취소도 그 자리를
                // 되돌리는 일이 된다. 컷씬은 onReveal을 부르지 않았으므로 능력치·애니메이터·
                // 기술은 애초에 손대지 않은 채다.
                if (EvolutionCutscene.Instance.WasCancelled)
                {
                    CurrentStageIndex--;
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowMessage(
                            previous.stageName + KoreanText.TopicParticle(previous.stageName)
                            + " 진화를 그만두었다.", 2.5f);
                }
            }
            else
            {
                // 컷씬 리소스가 없을 때의 예비 연출: 제자리 흰색 점멸
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage("어라...?! 몸이 빛나기 시작했다!", 1.6f);

                // 실제 시간으로 센다. 이 연출은 보스 보상 흐름 안에서도 도는데, 그때는
                // 시간이 멈춰 있어(timeScale 0) 스케일 시간으로 기다리면 영영 깨어나지 않는다.
                WaitForSecondsRealtime step = new WaitForSecondsRealtime(flashStepDuration);
                for (int i = 0; i < 6; i++)
                {
                    if (sr != null) sr.color = i % 2 == 0 ? Color.white * 3f : Color.white;
                    yield return step;
                }
                if (sr != null) sr.color = Color.white;

                ApplyStage(next, learnMove: true);

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

    /// <summary>
    /// 단계를 실제로 입힌다: 애니메이터·능력치 교체.
    /// 명세(gameplay-spec 6절)는 완전 회복이었으나, 보스 클리어가 너무 후해져서
    /// 비어 있는 체력의 일부만 채우도록 바꿨다 (<see cref="healMissingFraction"/>).
    /// </summary>
    /// <param name="learnMove">
    /// 진짜 진화인지. 기술 습득과 회복이 여기에 달려 있다 — 둘 다 <b>진화할 때만</b> 일어난다.
    ///
    /// 예전에는 이 안에서 무조건 <c>LearnNext()</c>를 불렀다. 진화가 이 함수를 부르는
    /// 유일한 길이던 시절에는 맞는 자리였지만, 판을 시작할 때 캐릭터의 1단계를 입히는
    /// 길(<see cref="LoadStages"/>)이 생기면서 <b>시작하자마자 기술을 하나 더 배우는</b>
    /// 문제가 됐다. 단계를 입히는 것과 진화하는 것은 다른 일이다.
    /// </param>
    private void ApplyStage(Stage next, bool learnMove)
    {
        // 결과 화면이 "쓰러진 그 모습"의 얼굴을 고를 수 있도록 단계를 기록에 남긴다.
        // 여기가 단계가 바뀌는 두 길(판 시작의 1단계 입히기, 실제 진화)이 모두 지나는 곳이다.
        RunStats.ReachedStage(CurrentStageIndex);

        Animator animator = GetComponent<Animator>();
        if (animator != null && next.animatorController != null)
            animator.runtimeAnimatorController = next.animatorController;

        // 최대치를 먼저 올린 뒤 회복해야, 늘어난 몫까지 회복 대상에 들어간다.
        // 연출 도중(예비 연출은 게임이 정지되지 않는다) 쓰러졌다면 회복 없이 최대치만 올린다.
        Health health = GetComponent<Health>();
        if (health != null)
        {
            health.SetMaxHealth(next.maxHealth, refill: false);
            // 회복은 진짜 진화일 때만 센다. 판을 시작하며 1단계를 입히는 길에서는 체력이
            // 이미 가득이라 회복량이 0이지만, 그것까지 "이 층에서 회복했다"로 세면
            // 1층을 넘어갈 때의 회복이 통째로 사라진다.
            if (!health.IsDead && learnMove)
            {
                health.HealMissingFraction(healMissingFraction);
                HealedThisFloor = true;
            }
        }

        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.SetMovePowers(next.movePowers, next.attackDamage, next.vineDamage);

        // 진화할 때마다 기술을 하나 더 배운다 (처음 둘 → 셋 → 넷).
        if (!learnMove) return;
        // 여기서부터가 "진짜 진화"다. 층당 한 번 제한은 이 순간에만 선다 —
        // B로 그만둔 진화는 여기까지 오지 않으므로 세지 않는다.
        EvolvedThisFloor = true;
        PlayerMoves moves = GetComponent<PlayerMoves>();
        LastLearnedMove = moves != null ? moves.LearnNext() : null;
    }
}
