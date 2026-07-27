using UnityEngine;

/// <summary>
/// 범용 이벤트 상호작용. 골드·회복·유물 보상을 주고 출구를 연다.
/// 2층 오아시스, 3층 보물상자 등에 사용한다.
/// </summary>
public class EventInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "E : 조사한다";
    [SerializeField, TextArea] private string resultMessage;
    [SerializeField] private int goldReward;
    [SerializeField] private int healAmount;
    [Tooltip("유물 보상을 줄지 여부. 어떤 유물이 나올지는 유물 등장 순서가 정한다.")]
    [SerializeField] private bool givesRelic;
    [Tooltip("특정 유물을 고정하고 싶을 때만 채운다. 비워 두면 등장 순서에서 다음 유물이 나온다.")]
    [SerializeField] private RelicData rewardRelic;
    [SerializeField] private ExitDoor exitDoor;
    [SerializeField] private bool disappearAfterUse;

    private bool used;

    public bool CanInteract => !used;
    public string Prompt => prompt;

    public void Interact(GameObject interactor)
    {
        if (used) return;
        used = true;

        if (!string.IsNullOrEmpty(resultMessage) && UIManager.Instance != null)
            UIManager.Instance.ShowMessage(resultMessage, 3f);

        if (goldReward > 0 && RunManager.Instance != null)
            RunManager.Instance.AddGold(goldReward);

        if (healAmount > 0)
        {
            Health health = interactor.GetComponent<Health>();
            if (health != null) health.Heal(healAmount);
        }

        if (givesRelic || rewardRelic != null)
            RelicManager.GrantReward(rewardRelic);

        if (exitDoor != null) exitDoor.SetOpen(true);

        if (disappearAfterUse)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
                sr.enabled = false;
            foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
                col.enabled = false;
        }
    }
}
