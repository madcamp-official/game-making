using UnityEngine;

/// <summary>
/// 3층 이벤트: 잉어킹이 폭포를 오르려 하는데, 호수 아래에는 보물상자가 보인다.
///
/// 확실한 보상(보물상자)과 도박(응원) 사이의 선택이다. 올려주기는 그 중간으로,
/// 유물 하나를 확정으로 주되 무엇이 나올지는 정해져 있다.
/// </summary>
public class MagikarpEvent : ChoiceEvent
{
    [Header("잉어킹을 올려준다")]
    [Tooltip("확정으로 주는 유물. 유물 더미에 넣지 않고 이 이벤트에서만 나온다.")]
    [SerializeField] private RelicData magikarpScale;

    [Header("보물상자를 챙긴다")]
    [SerializeField, Min(0)] private int chestGold = 80;

    [Header("잉어킹을 응원한다")]
    [SerializeField, Range(0, 100)] private int cheerSuccessChance = 50;
    [SerializeField, Min(1)] private int cheerRelicCount = 2;

    protected override EventPrompt BuildPrompt()
    {
        EventPrompt prompt = new EventPrompt
        {
            intro = "잉어킹이 폭포를 오르려 하고 있다. 그러나 호수 아래에 보물상자가 보이는 것 같습니다... " +
                    "어떻게 하시겠습니까?",
        };

        prompt.choices.Add(new EventChoice("잉어킹을 올려준다", HelpUp,
            EventEffectLine.Good("잉어킹의 비늘을 획득합니다.")));
        prompt.choices.Add(new EventChoice("보물상자를 챙긴다", TakeChest,
            EventEffectLine.Good("유물과 골드를 획득합니다.")));
        prompt.choices.Add(new EventChoice("잉어킹을 응원한다", Cheer,
            EventEffectLine.Good(cheerSuccessChance + "% 확률로 유물 " + cheerRelicCount + "개를 획득합니다."),
            EventEffectLine.Bad("실패하면 아무것도 얻지 못합니다.")));
        return prompt;
    }

    private EventOutcome HelpUp()
    {
        RelicManager.GrantReward(magikarpScale);
        return EventOutcome.Plain("잉어킹이 감사를 표합니다. 잉어킹의 비늘을 획득했습니다.");
    }

    private EventOutcome TakeChest()
    {
        RelicManager.GrantNonChoiceReward();
        if (RunManager.Instance != null) RunManager.Instance.AddGold(chestGold);
        return EventOutcome.Plain("당신은 호수 아래의 보물상자를 챙겼습니다. 유물과 골드를 획득했습니다. (+"
            + chestGold + "G)");
    }

    private EventOutcome Cheer()
    {
        if (Random.Range(0, 100) >= cheerSuccessChance)
        {
            return EventOutcome.Plain("잉어킹이 당신의 응원을 듣고 노력했으나 실패했습니다. " +
                                      "잉어킹은 이제 모든 힘을 쓴 것 같아 보입니다...");
        }

        for (int i = 0; i < cheerRelicCount; i++) RelicManager.GrantNonChoiceReward();
        return EventOutcome.Plain("잉어킹이 당신의 응원을 듣고 폭포를 오르는 데에 성공했습니다! " +
                                  "잉어킹이 폭포 위에서 유물을 떨어뜨려줍니다.");
    }
}
