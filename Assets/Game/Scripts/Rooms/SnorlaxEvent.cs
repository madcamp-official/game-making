using System.Collections;
using UnityEngine;

/// <summary>
/// 1층 이벤트: 잠만보가 길을 막고 자고 있다.
///
/// 깨우기는 실패해도 다시 시도할 수 있고, 시도할수록 피해와 성공 확률이 함께 오른다.
/// "안전하게 지나갈 것인가, 한 번 더 흔들어 볼 것인가"를 계속 다시 묻는 구조다.
/// </summary>
public class SnorlaxEvent : ChoiceEvent
{
    /// <summary>깨우기 시도마다의 (잃는 체력, 성공 확률%). 마지막 값에서 더 오르지 않는다.</summary>
    private static readonly (int damage, int chance)[] WakeAttempts =
    {
        (5, 25), (6, 35), (7, 45), (8, 55), (9, 65),
    };

    [Header("잠만보를 공격한다")]
    [SerializeField, Min(0)] private int attackSelfDamage = 20;
    [SerializeField, Min(0)] private int attackGoldReward = 50;

    [Header("연출")]
    [Tooltip("이벤트가 끝나면 잠만보가 비켜나며 사라진다.")]
    [SerializeField] private bool leaveAfterEvent = true;

    [Header("잠 표시")]
    [Tooltip("머리 위에 띄울 Zzz 그림 (Art/Environment/sleep). 비우면 표시가 뜨지 않는다.")]
    [SerializeField] private Sprite sleepSprite;

    [Tooltip("Zzz를 잠만보 몸 높이의 몇 할로 그릴지. 그림이 세로로 길어 높이를 기준으로 잡는다.")]
    [SerializeField, Range(0.1f, 1.5f)] private float sleepMarkHeightFraction = 0.7f;

    [Tooltip("몸 위쪽 끝에서 얼마나 띄울지 (몸 높이에 대한 비율).")]
    [SerializeField, Range(0f, 0.5f)] private float sleepMarkGapFraction = 0.06f;

    [Tooltip("좌우로 비켜 놓을 정도 (몸 폭에 대한 비율, 양수가 오른쪽). 웅크려 자는 그림이라 머리가 오른쪽 끝에 있다.")]
    [SerializeField, Range(-0.5f, 0.5f)] private float sleepMarkSideFraction = 0.2f;

    private SleepMark sleepMark;

    private int attempt;

    private void Start()
    {
        // 자고 있다는 것을 그림만으로 알 수 있게 한다. 말을 걸어 봐야 아는 것은 늦다.
        if (sleepSprite == null) return;
        SpriteRenderer body = GetComponentInChildren<SpriteRenderer>();
        sleepMark = SleepMark.Create(body, sleepSprite, sleepMarkHeightFraction, sleepMarkGapFraction,
                                     sleepMarkSideFraction);
    }

    /// <summary>더 이상 자고 있지 않다. 표시를 거둔다.</summary>
    private void WakeUp()
    {
        if (sleepMark != null) sleepMark.Dismiss();
        sleepMark = null;
    }

    protected override EventPrompt BuildPrompt() => MakePrompt(
        "잠만보가 잠을 자고 있습니다. 잠만보는 쉽게 일어날 것 같지 않습니다. 어떻게 하시겠습니까?");

    private EventPrompt MakePrompt(string intro)
    {
        (int damage, int chance) = WakeAttempts[Mathf.Min(attempt, WakeAttempts.Length - 1)];

        EventPrompt prompt = new EventPrompt { intro = intro };
        prompt.choices.Add(new EventChoice("잠만보를 깨워본다", TryWake,
            EventEffectLine.Bad("체력을 " + damage + " 잃습니다."),
            EventEffectLine.Good(chance + "% 확률로 잠만보가 유물을 줍니다.")));
        prompt.choices.Add(new EventChoice("잠만보를 공격한다", Attack,
            EventEffectLine.Bad("체력을 " + attackSelfDamage + " 잃습니다."),
            EventEffectLine.Good("잠만보가 " + attackGoldReward + "G를 떨어뜨립니다.")));
        prompt.choices.Add(new EventChoice("슬며시 지나간다", SneakPast));
        return prompt;
    }

    private EventOutcome TryWake()
    {
        (int damage, int chance) = WakeAttempts[Mathf.Min(attempt, WakeAttempts.Length - 1)];
        attempt++;

        // 무적 시간을 타지 않는다. 대사창이 시간을 멈춰 두므로 평범한 피해는 첫 번만 들어간다.
        Health health = PlayerHealth;
        if (health != null) health.TakeToll(damage);

        if (Random.Range(0, 100) >= chance)
        {
            // 실패. 다음 시도는 더 아프고 더 잘 통한다.
            return EventOutcome.Retry(MakePrompt(
                "힘껏 잠만보를 흔들었으나 잠만보가 일어나지 않습니다. 어떻게 하시겠습니까?"));
        }

        // 깨우기에 성공했다. 이제 자고 있지 않다.
        WakeUp();

        // 판을 통째로 결정짓는 구애 시리즈는 이런 도박 자리에서 나오지 않게 한다.
        RelicData relic = RelicManager.GrantNonChoiceReward();
        string result = relic != null
            ? "잠만보에게 " + relic.relicName + KoreanText.ObjectParticle(relic.relicName) + " 받았습니다!"
            : "하지만 잠만보는 줄 것이 남아 있지 않았습니다...";
        return EventOutcome.Say("하아암... 깨워줘서 고마워... 이거 줄게...", result, portrait);
    }

    private EventOutcome Attack()
    {
        // 두들겨 맞고 일어났다. 여기서도 잠은 깬다.
        WakeUp();

        Health health = PlayerHealth;
        if (health != null) health.TakeToll(attackSelfDamage);
        if (RunManager.Instance != null) RunManager.Instance.AddGold(attackGoldReward);

        return EventOutcome.Say("아야! 너 뭐야!",
            "잠만보가 당신을 짓밟고 지나갑니다. 잠만보가 골드를 떨어뜨리고 갔습니다. (+"
            + attackGoldReward + "G)", portrait);
    }

    private EventOutcome SneakPast()
    {
        sneakedPast = true;
        return EventOutcome.Plain("잠만보를 건드리지 않고 옆의 샛길로 지나갑니다.");
    }

    /// <summary>샛길로 지나가기를 골랐는가. 이 길만 방을 곧장 떠난다.</summary>
    private bool sneakedPast;

    protected override void OnFinished()
    {
        // 샛길로 지나갔다면 이 방에 더 볼 일이 없다. 잠만보가 비켜나는 것을 지켜본 뒤
        // 출구까지 걸어가게 하면, 이미 "지나갔다"고 말해 놓고 다시 걸으라는 셈이 된다.
        // 화면을 덮고 곧바로 다음 방으로 넘긴다 — 출구를 밟았을 때와 같은 연출이다.
        if (sneakedPast)
        {
            RoomTransition.Ensure().Go();
            return;
        }

        if (leaveAfterEvent) StartCoroutine(LeaveRoutine());
    }

    /// <summary>아래로 미끄러지듯 비켜나며 사라진다.</summary>
    private IEnumerator LeaveRoutine()
    {
        // 몸이 사라지는데 Zzz만 남아 떠 있으면 안 된다.
        WakeUp();

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        Vector3 start = transform.position;
        Vector3 end = start + new Vector3(0f, -3f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 1.2f;
            transform.position = Vector3.Lerp(start, end, t);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - t);
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
