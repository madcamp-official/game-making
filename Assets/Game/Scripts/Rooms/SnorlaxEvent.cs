using System.Collections;
using UnityEngine;

/// <summary>
/// 1층 이벤트: 잠만보가 길을 막고 있다. E로 깨우면 비켜주고 보상을 준다.
/// </summary>
public class SnorlaxEvent : MonoBehaviour, IInteractable
{
    [SerializeField] private ExitDoor exitDoor;
    [SerializeField] private int goldReward = 10;

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
        gameObject.SetActive(false);
    }
}
