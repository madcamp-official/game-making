using System.Collections;
using UnityEngine;

/// <summary>
/// 상점을 지키는 켈리몬. 방에 들어서면 한 번 자세를 잡고(Pose), 그 뒤로는 계속 고개를
/// 끄덕인다(Nod). 물건이 팔리면 숨을 크게 들이쉬고(DeepBreath) 다시 끄덕임으로 돌아간다.
///
/// 층마다 다른 상인이 아니라 <b>같은 켈리몬이 세 층에서 장사를 한다</b>는 것을 몸짓으로
/// 보여 주는 것이 목적이다. 끄덕임을 기본으로 두면 가만히 서 있어도 살아 있는 것으로 읽힌다.
///
/// 시간은 실제 시간(<see cref="WaitForSecondsRealtime"/>)으로 잰다. 상점에서 유물 획득
/// 팝업이 <see cref="Time.timeScale"/>을 0으로 세우는 순간이 있어서, 스케일 시간으로 재면
/// 팝업이 떠 있는 동안 몸짓이 그대로 얼어붙는다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ShopKeeper : MonoBehaviour
{
    [Tooltip("방에 들어섰을 때 잡는 자세. 이 시간만큼 유지한 뒤 끄덕임으로 넘어간다.")]
    [SerializeField] private string poseState = "Pose_0";
    [SerializeField, Min(0f)] private float poseHold = 0.5f;
    [Tooltip("평소 동작. 반복 재생이라 한 번 걸어 두면 계속 끄덕인다.")]
    [SerializeField] private string idleState = "Nod_0";
    [Tooltip("물건이 팔릴 때 한 번 하는 동작.")]
    [SerializeField] private string purchaseState = "DeepBreath_0";
    [Tooltip("구매 동작의 길이. 시트 재생 시간에 맞춘다 (DeepBreath 1.03초).")]
    [SerializeField, Min(0f)] private float purchaseDuration = 1.03f;

    private Animator animator;
    private Coroutine running;

    private void Awake() => animator = GetComponent<Animator>();

    private void Start() => running = StartCoroutine(Greet());

    /// <summary>이 방에서 물건이 팔렸다. <see cref="ShopItem"/>이 부른다.</summary>
    public void OnPurchased()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Thank());
    }

    private IEnumerator Greet()
    {
        Play(poseState);
        yield return new WaitForSecondsRealtime(poseHold);
        Play(idleState);
        running = null;
    }

    private IEnumerator Thank()
    {
        Play(purchaseState);
        yield return new WaitForSecondsRealtime(purchaseDuration);
        Play(idleState);
        running = null;
    }

    /// <summary>없는 상태 이름은 조용히 넘긴다 — 시트가 빠진 채로도 상점이 멈추지 않아야 한다.</summary>
    private void Play(string state)
    {
        if (animator == null || string.IsNullOrEmpty(state)) return;
        if (animator.HasState(0, Animator.StringToHash(state))) animator.Play(state, 0, 0f);
    }
}
