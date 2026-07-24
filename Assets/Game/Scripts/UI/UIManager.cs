using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 관리: 골드, 현재 방, 상호작용 힌트, 중앙 메시지.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private Text goldText;
    [SerializeField] private Text roomText;
    [SerializeField] private Text hintText;
    [SerializeField] private Text messageText;

    private Coroutine messageRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnGoldChanged += SetGold;
            SetGold(RunManager.Instance.Gold);
        }
        SetHint("");
        if (messageText != null) messageText.text = "";
    }

    public void SetGold(int gold)
    {
        if (goldText != null) goldText.text = "G " + gold;
    }

    public void SetRoomName(string label)
    {
        if (roomText != null) roomText.text = label;
    }

    public void SetHint(string hint)
    {
        if (hintText != null) hintText.text = hint;
    }

    public void ShowMessage(string message, float duration)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(message, duration));
    }

    private IEnumerator MessageRoutine(string message, float duration)
    {
        messageText.text = message;
        yield return new WaitForSeconds(duration);
        messageText.text = "";
        messageRoutine = null;
    }
}
