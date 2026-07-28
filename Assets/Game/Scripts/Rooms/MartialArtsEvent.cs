using UnityEngine;

/// <summary>
/// 2층 이벤트: 시라소몬과 홍수몬이 제자를 찾고 있다.
///
/// 세 선택지 모두 이롭기만 하다. 손해 없는 대신 "어느 쪽으로 클 것인가"를 정하는 자리다.
/// 여기서 얻는 강화는 유물이 아니라 <see cref="EventBuffs"/>에 쌓여 판이 끝날 때까지 남는다.
/// </summary>
public class MartialArtsEvent : ChoiceEvent
{
    [Header("스승")]
    [Tooltip("시라소몬 — 근접 강화")]
    [SerializeField] private Sprite hitmonleePortrait;
    [Tooltip("홍수몬 — 원거리 강화")]
    [SerializeField] private Sprite hitmonchanPortrait;

    [Header("강화 폭")]
    [SerializeField, Range(0f, 1f)] private float meleeBonus = 0.2f;
    [SerializeField, Range(0f, 1f)] private float rangedBonus = 0.2f;
    [SerializeField, Range(0f, 1f)] private float speedBonus = 0.15f;

    private static int Percent(float fraction) => Mathf.RoundToInt(fraction * 100f);

    protected override EventPrompt BuildPrompt()
    {
        EventPrompt prompt = new EventPrompt
        {
            intro = "시라소몬과 홍수몬이 제자를 찾고 있습니다. 둘의 제자가 된다면 강해질 수 있을 것 같습니다. " +
                    "어떻게 하시겠습니까?",
        };

        prompt.choices.Add(new EventChoice("시라소몬의 제자가 된다", LearnMelee,
            EventEffectLine.Good("근접공격이 " + Percent(meleeBonus) + "% 강해집니다.")));
        prompt.choices.Add(new EventChoice("홍수몬의 제자가 된다", LearnRanged,
            EventEffectLine.Good("원거리공격이 " + Percent(rangedBonus) + "% 강해집니다.")));
        prompt.choices.Add(new EventChoice("둘을 보고 독학한다", LearnAlone,
            EventEffectLine.Good("이동속도가 " + Percent(speedBonus) + "% 증가합니다.")));
        return prompt;
    }

    private EventOutcome LearnMelee()
    {
        EventBuffs.Instance.AddMeleeDamage(meleeBonus);
        return EventOutcome.Say("당연히 나를 골라야지!",
            "근접공격이 " + Percent(meleeBonus) + "% 강해졌습니다.", hitmonleePortrait);
    }

    private EventOutcome LearnRanged()
    {
        EventBuffs.Instance.AddRangedDamage(rangedBonus);
        return EventOutcome.Say("현명한 선택을 하셨군요.",
            "원거리공격이 " + Percent(rangedBonus) + "% 강해졌습니다.", hitmonchanPortrait);
    }

    private EventOutcome LearnAlone()
    {
        EventBuffs.Instance.AddMoveSpeed(speedBonus);
        return EventOutcome.Plain("당신은 둘의 전투를 보며 독학했습니다. 이동속도가 "
            + Percent(speedBonus) + "% 증가했습니다.");
    }
}
