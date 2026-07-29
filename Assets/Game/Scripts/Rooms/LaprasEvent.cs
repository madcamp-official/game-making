using System.Collections;
using UnityEngine;

/// <summary>
/// 3층 이벤트: 깊은 바다의 계곡을 라프라스가 50G에 건네준다.
///
/// 어떤 선택을 해도 계곡은 건너게 된다 — 이 방의 출구는 계곡 오른쪽에 있으므로
/// 건너지 못하는 선택지가 있으면 진행이 막힌다. 대신 "무엇을 지불하는가"가 갈린다:
/// 정가(50G)는 유물을 얹어 주고, 협상(20G)은 싸게 건너기만 하며, 거절은 몸으로 때운다(-5 체력).
/// </summary>
public class LaprasEvent : ChoiceEvent
{
    [Header("가격")]
    [SerializeField, Min(0)] private int ridePrice = 50;
    [SerializeField, Min(0)] private int negotiatedPrice = 20;
    [SerializeField, Min(0)] private int refuseDamage = 5;

    [Header("건너기 연출")]
    [Tooltip("라프라스 자신. 태워 줄 때 동쪽(Idle_2)으로 방향을 바꾼다.")]
    [SerializeField] private EventNpcPose lapras;
    [Tooltip("건넌 뒤 라프라스가 머무는 자리 (계곡 오른쪽).")]
    [SerializeField] private Transform laprasRideTarget;
    [Tooltip("플레이어가 내리는 자리. 혼자 헤엄칠 때도 여기로 나온다.")]
    [SerializeField] private Transform playerDropoff;
    [Tooltip("계곡을 막는 콜라이더. 건너는 동안만 끈다.")]
    [SerializeField] private Collider2D trenchCollider;
    [SerializeField, Min(0.05f)] private float boardDuration = 0.35f;
    [Tooltip("계곡을 건너는 데 걸리는 시간. 계곡 폭에 맞춰 Floor3EventSetup이 다시 계산한다.")]
    [SerializeField, Min(0.1f)] private float glideDuration = 1.5f;
    [Tooltip("타는 동안 플레이어를 라프라스보다 얼마나 위에 얹을지.")]
    [SerializeField] private float rideOffsetY = 0.45f;

    private enum Crossing { None, Ride, Alone }
    private Crossing crossing;

    protected override EventPrompt BuildPrompt() =>
        BuildPrompt("라프라스가 " + ridePrice + "원에 계곡을 태워서 건네주겠다고 제안합니다. 동의하시겠습니까?");

    private EventPrompt BuildPrompt(string intro)
    {
        EventPrompt prompt = new EventPrompt { intro = intro, portrait = portrait };
        prompt.choices.Add(new EventChoice("동의한다", Agree,
            EventEffectLine.Bad(ridePrice + "G를 지불합니다."),
            EventEffectLine.Good("무작위 유물을 하나 얻습니다.")));
        prompt.choices.Add(new EventChoice("협상을 한다", Negotiate,
            EventEffectLine.Bad(negotiatedPrice + "G를 지불합니다.")));
        prompt.choices.Add(new EventChoice("동의하지 않는다", Refuse,
            EventEffectLine.Bad("직접 헤엄쳐 건넙니다. 체력을 " + refuseDamage + " 잃습니다.")));
        return prompt;
    }

    private EventOutcome Agree()
    {
        if (RunManager.Instance == null || !RunManager.Instance.SpendGold(ridePrice))
            return NotEnoughGold();

        RelicData relic = RelicManager.GrantNonChoiceReward();
        crossing = Crossing.Ride;
        string reward = relic != null ? "'" + relic.relicName + "'을(를) 얻었습니다." : "";
        return EventOutcome.Say("탑승을 환영합니다! 꽉 잡으세요.",
            ridePrice + "G를 지불했습니다. 라프라스가 답례로 유물을 건네줍니다. " + reward, portrait);
    }

    private EventOutcome Negotiate()
    {
        if (RunManager.Instance == null || !RunManager.Instance.SpendGold(negotiatedPrice))
            return NotEnoughGold();

        crossing = Crossing.Ride;
        return EventOutcome.Say("...알겠습니다. " + negotiatedPrice + "원만 받을게요.",
            negotiatedPrice + "G를 지불했습니다. 라프라스가 시무룩한 얼굴로 등을 내어줍니다.", portrait);
    }

    private EventOutcome Refuse()
    {
        crossing = Crossing.Alone;
        Health health = PlayerHealth;
        if (health != null) health.TakeToll(refuseDamage);
        return EventOutcome.Plain("당신은 차가운 계곡을 직접 헤엄쳐 건넜습니다. (-" + refuseDamage + " 체력)");
    }

    private EventOutcome NotEnoughGold() =>
        EventOutcome.Retry(BuildPrompt("골드가 부족합니다. 라프라스가 안쓰러운 눈으로 바라봅니다. 어떻게 하시겠습니까?"));

    protected override void OnFinished()
    {
        if (crossing == Crossing.None || Player == null) return;
        StartCoroutine(CrossRoutine(crossing == Crossing.Ride));
    }

    private IEnumerator CrossRoutine(bool ride)
    {
        PlayerController controller = Player.GetComponent<PlayerController>();
        Rigidbody2D body = Player.GetComponent<Rigidbody2D>();
        if (body == null) yield break;

        if (controller != null) controller.ControlEnabled = false;
        body.linearVelocity = Vector2.zero;

        // 건너는 동안에는 물길과 라프라스 몸이 길을 막으면 안 된다.
        Collider2D ownCollider = GetComponent<Collider2D>();
        if (trenchCollider != null) trenchCollider.enabled = false;
        if (ownCollider != null) ownCollider.enabled = false;

        if (ride)
        {
            // 등에 태우고, 동쪽을 본 채 미끄러져 간다. 내린 뒤에도 오른쪽을 계속 본다.
            if (lapras != null) lapras.Play("Idle_2");
            yield return MoveBody(body, (Vector2)transform.position + Vector2.up * rideOffsetY, boardDuration);

            Vector2 from = transform.position;
            Vector2 to = laprasRideTarget != null ? (Vector2)laprasRideTarget.position : from;
            for (float t = 0f; t < glideDuration; t += Time.deltaTime)
            {
                Vector2 at = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / glideDuration));
                transform.position = at;
                body.position = at + Vector2.up * rideOffsetY;
                yield return null;
            }
            transform.position = to;
            yield return MoveBody(body, playerDropoff != null ? (Vector2)playerDropoff.position : to, boardDuration);
        }
        else
        {
            // 혼자 헤엄쳐 건넌다. 라프라스는 왼쪽에 그대로 남는다.
            if (playerDropoff != null)
                yield return MoveBody(body, playerDropoff.position, glideDuration);
        }

        if (ownCollider != null) ownCollider.enabled = true;
        if (trenchCollider != null) trenchCollider.enabled = true;
        if (controller != null) controller.ControlEnabled = true;
    }

    private static IEnumerator MoveBody(Rigidbody2D body, Vector2 to, float duration)
    {
        Vector2 from = body.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            body.position = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        body.position = to;
    }
}
