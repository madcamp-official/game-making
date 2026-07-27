using System.Collections;
using UnityEngine;

/// <summary>
/// 1층 이벤트: 잠만보가 길을 막고 있다. E로 깨우면 비켜주고 보상을 준다.
/// </summary>
public class SnorlaxEvent : MonoBehaviour, IInteractable
{
    [SerializeField] private ExitDoor exitDoor;
    [SerializeField] private int goldReward = 10;
    [Tooltip("유물 보상을 줄지 여부. 어떤 유물이 나올지는 유물 등장 순서가 정한다.")]
    [SerializeField] private bool givesRelic = true;
    [Tooltip("특정 유물을 고정하고 싶을 때만 채운다. 비워 두면 등장 순서에서 다음 유물이 나온다.")]
    [SerializeField] private RelicData rewardRelic;

    private bool done;

    public bool CanInteract => !done;
    public string Prompt => "E : 잠만보 깨우기";

    public void Interact(GameObject interactor)
    {
        if (done) return;
        done = true;
        StartCoroutine(WakeUpRoutine());
    }

    private IEnumerator WakeUpRoutine()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage("잠만보가 하품을 하더니 슬금슬금 비켜주었다... (+" + goldReward + "G)", 3f);
        if (RunManager.Instance != null)
            RunManager.Instance.AddGold(goldReward);

        // 아래로 미끄러지듯 비켜나며 사라진다.
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

        if (exitDoor != null) exitDoor.SetOpen(true);

        // 잠만보가 자리를 비키며 유물을 남긴다.
        if (givesRelic || rewardRelic != null)
            RelicManager.GrantReward(rewardRelic);

        gameObject.SetActive(false);
    }
}
