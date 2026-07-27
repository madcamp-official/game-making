using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD 우측 상단 유물 아이콘에 마우스를 올리면 이름과 설명을 띄운다 (슬레이 더 스파이어 방식).
///
/// 씬에 EventSystem이 없어 uGUI 포인터 이벤트를 쓸 수 없고, 넣더라도 게임 입력(좌클릭 공격)과
/// 겹칠 수 있다. 그래서 등록된 아이콘 사각형에 대해 마우스 위치를 직접 검사한다.
/// </summary>
public class RelicTooltip : MonoBehaviour
{
    private const int PanelWidth = 420;
    private const int Padding = 12;
    private const int GapToIcon = 10;
    private const int ScreenMargin = 12;

    private readonly List<RectTransform> iconRects = new List<RectTransform>();
    private readonly List<RelicData> iconRelics = new List<RelicData>();

    private RectTransform parentRect;
    private RectTransform panel;
    private Text nameText;
    private Text descText;
    private RelicData shown;
    private float panelWidth;

    /// <summary>등록된 아이콘 목록을 비운다. 유물 바를 다시 만들기 전에 호출한다.</summary>
    public void ClearTargets()
    {
        iconRects.Clear();
        iconRelics.Clear();
        Hide();
    }

    /// <summary>아이콘 하나를 호버 대상으로 등록한다.</summary>
    public void AddTarget(RectTransform icon, RelicData relic)
    {
        if (icon == null || relic == null) return;
        iconRects.Add(icon);
        iconRelics.Add(relic);
    }

    private void Awake()
    {
        // 이 오브젝트는 캔버스를 가득 채우도록 만들어져 있으므로 자기 자신이 화면 기준 사각형이다.
        parentRect = transform as RectTransform;
        Build();
        Hide();
    }

    private void Build()
    {
        panel = PixelUi.MakePanel(transform, "Panel");
        panel.pivot = new Vector2(1f, 1f);
        panel.sizeDelta = new Vector2(PanelWidth, 100f);

        Transform fill = panel.GetChild(0);
        nameText = PixelUi.MakeText(fill, "Name", 36, new Color(1f, 0.86f, 0.42f), TextAnchor.UpperLeft);
        descText = PixelUi.MakeText(fill, "Desc", 24, new Color(0.86f, 0.89f, 0.95f), TextAnchor.UpperLeft);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) { Hide(); return; }
        UpdateHover(mouse.position.ReadValue());
    }

    /// <summary>주어진 화면 좌표에 유물 아이콘이 있으면 툴팁을 띄우고, 없으면 감춘다.</summary>
    public void UpdateHover(Vector2 screenPos)
    {
        for (int i = 0; i < iconRects.Count; i++)
        {
            RectTransform icon = iconRects[i];
            if (icon == null) continue;
            // 화면 공간 오버레이 캔버스라 카메라는 null이다.
            if (!RectTransformUtility.RectangleContainsScreenPoint(icon, screenPos, null)) continue;
            Show(iconRelics[i], icon);
            return;
        }
        Hide();
    }

    private void Show(RelicData relic, RectTransform icon)
    {
        // 창 크기가 바뀌면 폭도 다시 잡아야 하므로 폭까지 같이 비교한다.
        float width = Mathf.Min(PanelWidth, Mathf.Max(240f, parentRect.rect.width - ScreenMargin * 2f));
        if (shown != relic || !Mathf.Approximately(width, panelWidth))
        {
            shown = relic;
            panelWidth = width;
            nameText.text = relic.relicName;
            descText.text = relic.description;

            panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);
            float y = PixelUi.StackFromTop(nameText, -Padding, Padding);
            y = PixelUi.StackFromTop(descText, y - Padding * 0.5f, Padding);
            panel.sizeDelta = new Vector2(width, -y + Padding);
        }

        panel.gameObject.SetActive(true);
        PlaceUnder(icon);
    }

    private void Hide()
    {
        shown = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    /// <summary>아이콘 바로 아래에, 오른쪽 끝을 아이콘에 맞춰 붙인다. 화면 밖으로 나가면 안쪽으로 민다.</summary>
    private void PlaceUnder(RectTransform icon)
    {
        if (parentRect == null) return;

        Vector3[] corners = new Vector3[4];
        icon.GetWorldCorners(corners);   // 0=좌하 1=좌상 2=우상 3=우하
        Vector2 screenBottomRight = RectTransformUtility.WorldToScreenPoint(null, corners[3]);

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screenBottomRight, null, out local)) return;

        // ScreenPointToLocalPointInRectangle은 부모 피벗 기준 좌표를 준다.
        panel.anchorMin = panel.anchorMax = parentRect.pivot;

        Rect area = parentRect.rect;
        float maxX = area.xMax - ScreenMargin;                      // 오른쪽 끝
        float minX = area.xMin + panelWidth + ScreenMargin;         // 왼쪽으로 더 밀면 잘린다
        float x = Mathf.Min(local.x, maxX);
        if (x < minX) x = Mathf.Min(minX, maxX);                    // 창이 패널보다 좁으면 오른쪽 우선
        float y = Mathf.Min(local.y - GapToIcon, area.yMax - ScreenMargin);
        panel.anchoredPosition = new Vector2(x, y);
    }
}
