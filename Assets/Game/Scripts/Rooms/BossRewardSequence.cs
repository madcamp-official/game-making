using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 보스를 잡은 뒤의 보상을 <b>하나씩 차례로</b> 보여 주는 흐름.
///
/// 예전에는 진화 연출·기술 습득 안내·유물 획득 팝업이 같은 순간에 한꺼번에 터져서 무엇을
/// 얻었는지 알아볼 수가 없었다. 셋을 <b>진화 → 새 기술 → 유물</b> 순으로 한 줄로 세우고,
/// 각 장을 플레이어가 확인해야 다음으로 넘어가게 한다. 일어나지 않은 보상은 그 장이 통째로
/// 빠진다 — 행복의알로 이미 진화했다면 진화와 기술 습득을 건너뛰고 유물부터 시작한다.
///
/// <b>일시정지와 조작 잠금은 여기서만 건드린다.</b> 진화 컷씬과 유물 선택 창도 각자
/// <see cref="Time.timeScale"/>을 0으로 세웠다 되돌리는데, 그것들은 이 흐름 <i>안에서만</i>
/// 돌기 때문에 되돌려 놓는 값이 결국 이쪽이 세운 0이다. 바깥으로 나가는 값은
/// <see cref="Run"/>의 finally가 한 번만 되돌린다.
///
/// 출구도 이 흐름이 연다. 마지막 장을 확인하기 전에 열려 있으면 보상을 읽지 않고
/// 다음 방으로 걸어 나갈 수 있다.
/// </summary>
public class BossRewardSequence : MonoBehaviour
{
    /// <summary>보상 흐름이 도는 중인지. 다른 창·안내가 여기에 끼어들지 않으려고 본다.</summary>
    public static bool IsRunning { get; private set; }

    private static BossRewardSequence instance;

    /// <summary>정적 값이라 판이 바뀌어도 살아남는다. 판마다 초기화한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        IsRunning = false;
        instance = null;
    }

    /// <summary>마지막 타격과 죽는 연출이 끝날 시간. 이 동안은 시간이 그대로 흐른다.</summary>
    private const float SettleDuration = 0.5f;

    private const int PanelWidth = 660;
    private const int Padding = 20;
    private const int IconSize = 64;

    private RectTransform panel;
    private Image dim;
    private Image icon;
    private Text headerText;
    private Text titleText;
    private Text bodyText;
    private Text hintText;

    private float savedTimeScale = 1f;

    /// <summary>캔버스 아래에 만들어 붙인다 (<see cref="UIManager"/>가 호출).</summary>
    public static BossRewardSequence Create(Transform canvasRoot)
    {
        GameObject go = new GameObject("BossRewardSequence", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(canvasRoot, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go.AddComponent<BossRewardSequence>();
    }

    /// <summary>
    /// 보상 흐름을 시작한다. 이미 돌고 있거나 화면을 만들 수 없으면 false —
    /// 그때는 부르는 쪽이 예전 방식으로 보상을 지급해야 보상을 잃지 않는다.
    /// </summary>
    public static bool Begin(Transform room, RelicData fixedRelic, ExitDoor exitDoor)
    {
        if (instance == null || IsRunning) return false;
        instance.StartCoroutine(instance.Run(room, fixedRelic, exitDoor));
        return true;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        // timeScale은 씬이 바뀌어도 유지되는 전역값이다. 흐름이 도는 채로 씬이 내려가면
        // 다음 씬이 멈춘 채 시작한다.
        if (IsRunning)
        {
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
            IsRunning = false;
        }
        if (instance == this) instance = null;
    }

    // ---------------------------------------------------------------- 흐름

    private IEnumerator Run(Transform room, RelicData fixedRelic, ExitDoor exitDoor)
    {
        IsRunning = true;
        SetVisible(false);

        PlayerController player = FindAnyObjectByType<PlayerController>();
        savedTimeScale = Time.timeScale;

        try
        {
            // 1. 뒷정리. 아직 시간은 흐른다 — 마지막 타격과 죽는 연출이 끝나야 보상이 시작된다.
            SetControl(player, false);
            EnemyEffect.ClearUnder(room);
            yield return new WaitForSecondsRealtime(SettleDuration);
            // 정리 중에 수명이 다해 남은 것이 있을 수 있어 한 번 더 훑는다.
            EnemyEffect.ClearUnder(room);

            Time.timeScale = 0f;

            // 2. 진화. 조건을 못 채웠으면(행복의알로 이미 이 층에서 진화했다면) 통째로 건너뛴다.
            MoveType? learned = null;
            PlayerEvolution evolution = player != null ? player.GetComponent<PlayerEvolution>() : null;
            if (evolution != null && evolution.CanEvolve)
            {
                evolution.Evolve();
                while (evolution.IsEvolving) yield return null;

                learned = evolution.LastLearnedMove;
                // 진화 연출은 끝내면서 조작을 되살린다. 보상은 아직 남았으므로 다시 잠근다.
                SetControl(player, false);
            }

            // 3. 새로 배운 기술. 진화해도 기술 칸이 다 찼으면 배울 것이 없다.
            if (learned.HasValue)
            {
                MoveType move = learned.Value;
                yield return ShowPage("새로운 기술!", null, MoveInfo.NameOf(move),
                    "조작 : " + MoveInfo.KeyLabelOf(move) + "   ·   " + MoveInfo.TagOf(move)
                    + "\n" + MoveInfo.SummaryOf(move));
            }

            // 4. 유물. 다우징머신이 있으면 둘 중 하나를 고르고, 없으면 하나를 크게 보여준다.
            //    마지막 장이므로 이걸 확인하면 곧바로 조작이 돌아오고 출구가 열린다.
            List<RelicData> offered = RelicManager.DrawBossReward(fixedRelic);
            if (offered.Count >= 2)
            {
                yield return ChooseRelic(offered[0], offered[1]);
            }
            else if (offered.Count == 1)
            {
                RelicData gained = offered[0];
                Grant(gained);
                yield return ShowPage("유물 획득!", gained.icon, gained.relicName, gained.description);
            }
        }
        finally
        {
            SetVisible(false);
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
            SetControl(player, true);
            // 출구는 마지막 장을 확인한 뒤에야 열린다.
            if (exitDoor != null) exitDoor.SetOpen(true);
            IsRunning = false;
        }
    }

    /// <summary>다우징머신: 두 유물 중 하나를 고르게 하고, 고르지 않은 쪽은 더미로 돌려보낸다.</summary>
    private IEnumerator ChooseRelic(RelicData first, RelicData second)
    {
        // 선택 창이 뜨는 동안에는 이 화면을 접는다. 보상 화면은 언제나 하나만 보여야 한다.
        SetVisible(false);
        bool opened = UIManager.Instance != null &&
                      UIManager.Instance.ShowRelicChoice(first, second, relic =>
                      {
                          Grant(relic);
                          if (RelicManager.Instance != null)
                              RelicManager.Instance.ReturnToPool(relic == first ? second : first);
                      });

        if (opened)
        {
            while (RelicChoicePanel.IsOpen) yield return null;
        }
        else
        {
            // 창을 띄울 수 없으면 보상을 잃지 않도록 첫 번째를 그냥 준다.
            Grant(first);
            if (RelicManager.Instance != null) RelicManager.Instance.ReturnToPool(second);
            yield return ShowPage("유물 획득!", first.icon, first.relicName, first.description);
        }
    }

    private static void Grant(RelicData relic)
    {
        // 획득 팝업은 끈다. 이 흐름이 전용 화면으로 이미 보여 주므로, 켜 두면 둘이 겹친다.
        if (RelicManager.Instance != null) RelicManager.Instance.AddRelic(relic, announce: false);
    }

    private static void SetControl(PlayerController player, bool enabled)
    {
        if (player != null) player.ControlEnabled = enabled;
    }

    // ---------------------------------------------------------------- 한 장 보여주기

    /// <summary>한 장을 띄우고 플레이어가 확인할 때까지 기다린다. 저절로 사라지지 않는다.</summary>
    private IEnumerator ShowPage(string header, Sprite pageIcon, string title, string body)
    {
        // 켜고 나서 잰다. 꺼져 있는 오브젝트는 위쪽 캔버스를 찾지 못해 글자 높이가 틀어질 수 있다.
        SetVisible(true);
        Layout(header, pageIcon, title, body);

        // 마지막 적을 잡은 그 클릭이 첫 장을 그대로 넘겨 버리지 않게 한 프레임 흘린다.
        yield return null;
        while (!ConfirmPressed()) yield return null;

        SetVisible(false);
        // 한 장을 넘긴 입력이 다음 장까지 잇달아 넘기지 않도록 한 프레임 더 흘린다.
        yield return null;
    }

    private static bool ConfirmPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    // ---------------------------------------------------------------- 화면 구성

    private void Build()
    {
        GameObject dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(transform, false);
        dim = dimGo.AddComponent<Image>();
        dim.sprite = PrimitiveSprites.Square;
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        dim.raycastTarget = false;
        RectTransform dimRt = dim.rectTransform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        panel = PixelUi.MakePanel(transform, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelWidth, 240f);

        Transform fill = panel.GetChild(0);

        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(fill, false);
        icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        icon.rectTransform.pivot = new Vector2(0.5f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);

        headerText = PixelUi.MakeText(fill, "Header", 24, new Color(0.72f, 0.78f, 0.88f),
                                      TextAnchor.UpperCenter);
        titleText = PixelUi.MakeText(fill, "Title", 36, new Color(1f, 0.86f, 0.42f),
                                     TextAnchor.UpperCenter);
        bodyText = PixelUi.MakeText(fill, "Body", 24, Color.white, TextAnchor.UpperCenter);
        hintText = PixelUi.MakeText(fill, "Hint", 12, new Color(0.8f, 0.8f, 0.85f, 0.8f),
                                    TextAnchor.UpperCenter);
        hintText.text = "아무 키나 눌러 계속";
    }

    /// <summary>한 장의 내용을 채우고 패널 높이를 글자에 맞춘다.</summary>
    private void Layout(string header, Sprite pageIcon, string title, string body)
    {
        headerText.text = header;
        titleText.text = title;
        bodyText.text = body;
        icon.sprite = pageIcon;
        icon.enabled = pageIcon != null;

        // 창이 좁으면 패널도 같이 줄인다. 글자 높이는 폭이 정해진 뒤에 재야 맞다.
        Rect area = ((RectTransform)transform).rect;
        float width = Mathf.Min(PanelWidth, Mathf.Max(280f, area.width - Padding * 2f));
        panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);

        float gap = Padding * 0.5f;
        float y = -Padding;

        if (icon.enabled)
        {
            icon.rectTransform.anchoredPosition = new Vector2(0f, y);
            y -= IconSize + gap;
        }
        y = PixelUi.StackFromTop(headerText, y, Padding) - gap * 0.5f;
        y = PixelUi.StackFromTop(titleText, y, Padding) - gap;
        y = PixelUi.StackFromTop(bodyText, y, Padding) - gap;
        y = PixelUi.StackFromTop(hintText, y, Padding);

        panel.sizeDelta = new Vector2(width, -y + Padding);
    }

    private void SetVisible(bool visible)
    {
        if (dim != null) dim.gameObject.SetActive(visible);
        if (panel != null) panel.gameObject.SetActive(visible);
    }
}
