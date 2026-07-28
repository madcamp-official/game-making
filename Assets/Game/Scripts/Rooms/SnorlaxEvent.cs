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
    [SerializeField, Min(0)] private int attackSelfDamage = 15;
    [SerializeField, Min(0)] private int attackGoldReward = 50;

    [Header("연출")]
    [Tooltip("이벤트가 끝나면 잠만보가 비켜나며 사라진다.")]
    [SerializeField] private bool leaveAfterEvent = true;

    private int attempt;

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
            EventEffectLine.Good("잠만보가 골드를 떨어뜨립니다.")));
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

        // 판을 통째로 결정짓는 구애 시리즈는 이런 도박 자리에서 나오지 않게 한다.
        RelicData relic = RelicManager.GrantNonChoiceReward();
        string result = relic != null
            ? "잠만보에게 " + relic.relicName + KoreanText.ObjectParticle(relic.relicName) + " 받았습니다!"
            : "하지만 잠만보는 줄 것이 남아 있지 않았습니다...";
        return EventOutcome.Say("하아암... 깨워줘서 고마워... 이거 줄게...", result, portrait);
    }

    private EventOutcome Attack()
    {
        Health health = PlayerHealth;
        if (health != null) health.TakeToll(attackSelfDamage);
        if (RunManager.Instance != null) RunManager.Instance.AddGold(attackGoldReward);

        return EventOutcome.Say("아야! 너 뭐야!",
            "잠만보가 당신을 짓밟고 지나갑니다. 잠만보가 골드를 떨어뜨리고 갔습니다. (+"
            + attackGoldReward + "G)", portrait);
    }

    private EventOutcome SneakPast() =>
        EventOutcome.Plain("잠만보를 건드리지 않고 옆의 샛길로 지나갑니다.");

    protected override void OnFinished()
    {
        if (leaveAfterEvent) StartCoroutine(LeaveRoutine());
    }

    /// <summary>아래로 미끄러지듯 비켜나며 사라진다.</summary>
    private IEnumerator LeaveRoutine()
    {
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
