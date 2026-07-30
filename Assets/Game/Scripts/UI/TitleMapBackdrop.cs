using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 뒤로 <b>모든 방을 차례로 흘려 보내는</b> 배경. 방 스틸을 가로로 이어 붙여 끊임없이
/// 왼쪽으로 밀고, 그 위에 어두운 막을 덮어 로고와 메뉴 글자가 살아 있게 한다.
///
/// 그림은 에디터에서 미리 구워 둔 것이다(<c>TitleMapSetup</c>). 방 프리팹을 실제로 띄우면
/// 적이 돌아다니고 탄을 쏘므로, 타이틀 동안 방을 올리지 않기로 한 결정을 되돌리는 셈이 된다.
///
/// 흐르는 방식은 <b>고리</b>다. 왼쪽으로 밀려 화면을 벗어난 장은 오른쪽 끝으로 돌아가므로
/// 마지막 방 다음에 첫 방이 이어 붙고, 도중에 멈추거나 되감기는 순간이 없다.
///
/// 시간은 <see cref="Time.unscaledTime"/>으로 센다. 타이틀은 <see cref="Time.timeScale"/>이
/// 0인 채로 떠 있어서, 스케일 시간으로 재면 배경이 멈춰 선다.
/// </summary>
public class TitleMapBackdrop : MonoBehaviour
{
    /// <summary>구워 둔 스틸 한 장의 크기 (<c>TitleMapSetup</c>과 같은 값).</summary>
    private const float SourceWidth = 480f;
    private const float SourceHeight = 270f;

    /// <summary>화면에서 스틸 한 장이 차지하는 크기. <see cref="Create"/>가 화면 높이에서 정한다.</summary>
    private float pageWidth = SourceWidth;
    private float pageHeight = SourceHeight;

    /// <summary>흐르는 속도(초당 화면 픽셀). 한 방이 지나가는 데 12초쯤 걸린다.</summary>
    private const float Speed = 160f;

    /// <summary>
    /// 위에 덮는 막. 방 그림이 그대로 보이면 타일 무늬가 눈을 끌어 메뉴 글자를 읽기 어렵다.
    /// 남색 쪽으로 눌러 두면 예전 단색 배경의 분위기가 남는다.
    /// </summary>
    private static readonly Color Veil = new Color(0.04f, 0.06f, 0.12f, 0.62f);

    /// <summary>스틸을 한 장도 찾지 못했을 때 깔 색. 예전 타이틀 배경과 같다.</summary>
    private static readonly Color Fallback = new Color(0.05f, 0.07f, 0.14f);

    private readonly List<RectTransform> pages = new List<RectTransform>();
    private float total;

    /// <summary>
    /// 배경을 만들어 <paramref name="parent"/>의 맨 아래에 깐다. 스틸이 없으면 단색으로 버틴다 —
    /// 굽지 않은 사람의 화면이 검게 비는 것보다 낫다.
    /// </summary>
    public static TitleMapBackdrop Create(Transform parent)
    {
        var go = new GameObject("MapBackdrop", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.SetAsFirstSibling();
        PmdUi.Stretch(rt);

        var backdrop = go.AddComponent<TitleMapBackdrop>();

        // 흐르는 장들이 화면 밖으로 새지 않게 가둔다. 없으면 로고 위까지 그림이 지나간다.
        go.AddComponent<RectMask2D>();

        Sprite[] maps = Resources.LoadAll<Sprite>("UI/TitleMaps");
        if (maps == null || maps.Length == 0)
        {
            var solid = go.AddComponent<Image>();
            solid.sprite = PrimitiveSprites.Square;
            solid.color = Fallback;
            solid.raycastTarget = false;
            return backdrop;
        }

        // 파일 이름에 번호가 박혀 있다(TitleMap00…). Resources.LoadAll의 순서는 약속된 것이
        // 아니므로 이름으로 정렬해 층·방 순서를 되찾는다.
        System.Array.Sort(maps, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        // ⚠️ 늘리는 배수는 <b>정수</b>여야 한다. 픽셀 아트를 소수배로 늘리면 타일 한 칸이
        // 자리마다 다른 픽셀 수로 그려져 격자가 울렁인다. 화면을 덮을 만큼 올림해서 잡으므로
        // 위아래가 조금 잘리는데, 배경이 검게 비는 것보다 잘리는 편이 낫다.
        float canvasHeight = ((RectTransform)parent).rect.height;
        int scale = Mathf.Max(1, Mathf.CeilToInt(canvasHeight / SourceHeight));
        backdrop.pageWidth = SourceWidth * scale;
        backdrop.pageHeight = SourceHeight * scale;

        foreach (Sprite map in maps)
        {
            var pageGo = new GameObject(map.name, typeof(RectTransform));
            var page = (RectTransform)pageGo.transform;
            page.SetParent(rt, false);
            // 왼쪽 가운데를 기준으로 삼는다 — x 하나만 굴리면 자리가 정해진다.
            page.anchorMin = page.anchorMax = new Vector2(0f, 0.5f);
            page.pivot = new Vector2(0f, 0.5f);
            page.sizeDelta = new Vector2(backdrop.pageWidth, backdrop.pageHeight);

            var image = pageGo.AddComponent<Image>();
            image.sprite = map;
            image.raycastTarget = false;
            backdrop.pages.Add(page);
        }
        backdrop.total = backdrop.pages.Count * backdrop.pageWidth;
        backdrop.Layout();

        var veil = PmdUi.MakeSliced(rt, "Veil", null);
        veil.color = Veil;
        PmdUi.Stretch(veil.rectTransform);

        return backdrop;
    }

    private void Update() => Layout();

    private void Layout()
    {
        if (pages.Count == 0 || total <= 0f) return;

        float scrolled = Time.unscaledTime * Speed % total;
        for (int i = 0; i < pages.Count; i++)
        {
            float x = i * pageWidth - scrolled;
            // 왼쪽으로 완전히 지나간 장은 줄의 끝으로 돌려보낸다. 이것이 고리를 만든다.
            if (x <= -pageWidth) x += total;
            pages[i].anchoredPosition = new Vector2(x, 0f);
        }
    }
}
