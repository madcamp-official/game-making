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
    [SerializeField] private RectTransform relicBar;

    private Coroutine messageRoutine;
    private RelicTooltip relicTooltip;
    private RelicPopup relicPopup;

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
        BuildRelicUi();
        if (RelicManager.Instance != null)
        {
            RelicManager.Instance.OnRelicsChanged += RefreshRelics;
            RefreshRelics();
        }
        SetHint("");
        if (messageText != null) messageText.text = "";
    }

    private void RefreshRelics()
    {
        if (relicBar == null || RelicManager.Instance == null) return;

        for (int i = relicBar.childCount - 1; i >= 0; i--)
            Destroy(relicBar.GetChild(i).gameObject);

        if (relicTooltip != null) relicTooltip.ClearTargets();

        int index = 0;
        foreach (RelicData relic in RelicManager.Instance.Relics)
        {
            GameObject iconGo = new GameObject("Relic_" + relic.relicName);
            iconGo.transform.SetParent(relicBar, false);
            Image image = iconGo.AddComponent<Image>();
            image.sprite = relic.icon;
            image.preserveAspect = true;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(56, 56);
            rt.anchoredPosition = new Vector2(-index * 64, 0);
            index++;

            if (relicTooltip != null) relicTooltip.AddTarget(rt, relic);
        }
    }

    /// <summary>플레이어 체력바, 유물 획득 팝업, 호버 툴팁을 캔버스 아래에 만들어 둔다.</summary>
    private void BuildRelicUi()
    {
        Transform canvasRoot = relicBar != null ? relicBar.parent : transform;

        PlayerHealthHud.Create(canvasRoot);
        relicPopup = MakeFullScreenChild(canvasRoot, "RelicPopup").AddComponent<RelicPopup>();

        // 툴팁은 다른 HUD 요소 위에 그려져야 한다.
        GameObject tooltipGo = MakeFullScreenChild(canvasRoot, "RelicTooltip");
        tooltipGo.transform.SetAsLastSibling();
        relicTooltip = tooltipGo.AddComponent<RelicTooltip>();
    }

    /// <summary>화면 전체를 덮는 빈 컨테이너. 안쪽 패널이 화면 기준으로 배치될 수 있게 한다.</summary>
    private static GameObject MakeFullScreenChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    /// <summary>유물 획득 시 전용 패널로 이름과 설명을 크게 보여준다.</summary>
    public void ShowRelicAcquired(RelicData relic, float duration)
    {
        if (relicPopup != null) relicPopup.Show(relic, duration);
    }

    private int lastGold = int.MinValue;
    private string lastHint;

    public void SetGold(int gold)
    {
        if (goldText == null || gold == lastGold) return;
        lastGold = gold;
        goldText.text = "G " + gold;
    }

    public void SetRoomName(string label)
    {
        if (roomText != null) roomText.text = label;
    }

    // 같은 힌트가 유지되는 동안 Text 재대입(캔버스 리빌드)을 피한다.
    public void SetHint(string hint)
    {
        if (hintText == null || hint == lastHint) return;
        lastHint = hint;
        hintText.text = hint;
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
