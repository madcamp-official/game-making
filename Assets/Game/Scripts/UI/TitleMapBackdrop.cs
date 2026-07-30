using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 뒤로 <b>층마다 하나씩의 맵을 차례로 보여 주는</b> 배경. 한 장이 천천히 옆으로 밀려가고,
/// 끝에 닿으면 다음 장으로 <b>겹쳐 넘어간다</b>. 그 위에 어두운 막을 덮어 로고와 메뉴 글자가
/// 살아 있게 한다.
///
/// 그림은 에디터에서 미리 구워 둔 것이다(<c>TitleMapSetup</c>). 맵 프리팹을 실제로 띄우면
/// 적이 돌아다니고 탄을 쏘므로, 타이틀 동안 방을 올리지 않기로 한 결정을 되돌리는 셈이 된다.
///
/// <b>넘어가는 사이에 검은 화면이 없다.</b> 나가는 장을 지우고 들어오는 장을 띄우면 그 사이가
/// 비는데, 대신 <b>들어오는 장을 나가는 장 앞에 겹쳐 놓고 서서히 드러낸다</b>. 어느 순간에도
/// 화면을 채우는 그림이 하나는 있으므로 검은 틈이 생길 자리가 없다.
///
/// 시간은 <see cref="Time.unscaledTime"/>으로 센다. 타이틀은 <see cref="Time.timeScale"/>이
/// 0인 채로 떠 있어서, 스케일 시간으로 재면 배경이 멈춰 선다.
/// </summary>
public class TitleMapBackdrop : MonoBehaviour
{
    /// <summary>구워 둔 스틸 한 장의 크기 (<c>TitleMapSetup</c>과 같은 값).</summary>
    private const float SourceWidth = 720f;
    private const float SourceHeight = 270f;

    /// <summary>한 장이 자기 몫을 다 미는 데 걸리는 시간.</summary>
    private const float PanDuration = 22f;

    /// <summary>다음 장이 드러나는 시간.</summary>
    private const float FadeDuration = 1.4f;

    /// <summary>
    /// 미는 폭을 남은 여유보다 조금 적게 잡는다. 넘어가는 동안 나가는 장도 계속 밀려야
    /// 움직임이 끊기지 않는데, 여유를 다 쓰면 그때 그림의 오른쪽 끝이 화면에 들어온다.
    /// </summary>
    private const float PanMargin = 80f;

    /// <summary>
    /// 위에 덮는 막. 맵이 그대로 보이면 타일 무늬가 눈을 끌어 메뉴 글자를 읽기 어렵다.
    /// 남색 쪽으로 눌러 두면 예전 단색 배경의 분위기가 남는다.
    /// </summary>
    private static readonly Color Veil = new Color(0.03f, 0.05f, 0.10f, 0.74f);

    /// <summary>스틸을 한 장도 찾지 못했을 때 깔 색. 예전 타이틀 배경과 같다.</summary>
    private static readonly Color Fallback = new Color(0.05f, 0.07f, 0.14f);

    private readonly List<Image> pages = new List<Image>();

    /// <summary>화면에서 스틸 한 장이 차지하는 크기. <see cref="Create"/>가 화면 높이에서 정한다.</summary>
    private float pageWidth = SourceWidth;

    /// <summary>한 장이 밀려가는 거리.</summary>
    private float panRange;

    /// <summary>지금 맨 앞에 있는 장. 순서를 바꿀 때만 손대므로 매 프레임 건드리지 않는다.</summary>
    private int frontPage = -1;

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

        // 미는 장이 화면 밖으로 새지 않게 가둔다. 없으면 로고 위까지 그림이 지나간다.
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
        // 아니므로 이름으로 정렬해 층 순서를 되찾는다.
        System.Array.Sort(maps, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        // ⚠️ 늘리는 배수는 <b>정수</b>여야 한다. 픽셀 아트를 소수배로 늘리면 타일 한 칸이
        // 자리마다 다른 픽셀 수로 그려져 격자가 울렁인다. 화면을 덮을 만큼 올림해서 잡으므로
        // 위아래가 조금 잘리는데, 배경이 검게 비는 것보다 잘리는 편이 낫다.
        var parentRt = (RectTransform)parent;
        int scale = Mathf.Max(1, Mathf.CeilToInt(parentRt.rect.height / SourceHeight));
        backdrop.pageWidth = SourceWidth * scale;
        float pageHeight = SourceHeight * scale;
        backdrop.panRange = Mathf.Max(0f, backdrop.pageWidth - parentRt.rect.width - PanMargin);

        // 장들을 따로 담는 그릇. 넘어갈 때 장의 앞뒤 순서를 바꾸는데, 막과 한 부모 아래에
        // 있으면 앞으로 나온 장이 막까지 덮어 버린다.
        var pagesRoot = new GameObject("Pages", typeof(RectTransform));
        var pagesRt = (RectTransform)pagesRoot.transform;
        pagesRt.SetParent(rt, false);
        PmdUi.Stretch(pagesRt);

        foreach (Sprite map in maps)
        {
            var pageGo = new GameObject(map.name, typeof(RectTransform));
            var page = (RectTransform)pageGo.transform;
            page.SetParent(pagesRt, false);
            // 왼쪽 가운데를 기준으로 삼는다 — x 하나만 굴리면 자리가 정해진다.
            page.anchorMin = page.anchorMax = new Vector2(0f, 0.5f);
            page.pivot = new Vector2(0f, 0.5f);
            page.sizeDelta = new Vector2(backdrop.pageWidth, pageHeight);

            var image = pageGo.AddComponent<Image>();
            image.sprite = map;
            image.raycastTarget = false;
            backdrop.pages.Add(image);
        }
        backdrop.Layout();

        var veil = PmdUi.MakeSliced(rt, "Veil", null);
        veil.color = Veil;
        PmdUi.Stretch(veil.rectTransform);

        return backdrop;
    }

    private void Update() => Layout();

    /// <summary>
    /// 지금 시각에 맞춰 장들의 자리와 짙기를 정한다.
    ///
    /// 한 장이 <see cref="PanDuration"/> 동안 자기 몫을 밀고, 그 시간이 끝나면 다음 장으로
    /// 넘어간다. 넘어가는 <see cref="FadeDuration"/> 동안 <b>나가는 장은 그대로 짙게 남아
    /// 있고 들어오는 장이 그 앞에서 드러난다</b> — 그래서 화면이 빌 틈이 없다.
    /// </summary>
    private void Layout()
    {
        int count = pages.Count;
        if (count == 0) return;

        float now = Time.unscaledTime;
        int index = (int)(now / PanDuration) % count;
        float local = now - Mathf.Floor(now / PanDuration) * PanDuration;
        int previous = (index - 1 + count) % count;

        for (int i = 0; i < count; i++) SetAlpha(pages[i], 0f);

        // 들어오는 장은 처음 FadeDuration 동안 서서히 드러난다. 장이 하나뿐이면 넘어갈 곳이
        // 없으므로 드러내지 않는다 — 뒤에 아무것도 없어서 그대로 검은 화면이 된다.
        float appearing = count > 1 && FadeDuration > 0f ? Mathf.Clamp01(local / FadeDuration) : 1f;
        SetAlpha(pages[index], appearing);
        pages[index].rectTransform.anchoredPosition =
            new Vector2(-panRange * (local / PanDuration), 0f);

        if (appearing < 1f && count > 1)
        {
            // 나가는 장. 짙기를 그대로 두고 계속 밀어 움직임을 잇는다. 밀 자리는
            // PanMargin이 남겨 둔 몫에서 꺼내 쓴다.
            SetAlpha(pages[previous], 1f);
            pages[previous].rectTransform.anchoredPosition =
                new Vector2(-panRange - PanMargin * (local / FadeDuration), 0f);
        }

        // 들어오는 장이 앞에 와야 "드러나는" 것이 된다. 뒤에 있으면 나가는 장에 가려
        // 아무 일도 일어나지 않는다. 순서가 바뀔 때만 손댄다.
        if (frontPage != index)
        {
            frontPage = index;
            pages[index].transform.SetAsLastSibling();
        }
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
