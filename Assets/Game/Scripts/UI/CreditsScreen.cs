using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 크레딧 두루마리. 영화 끝처럼 아래에서 위로 한 번 흘러가고, 다 지나가면 타이틀로 돌아온다.
///
/// <b>글은 <c>docs/FINALCREDITS.md</c>가 그대로 맡는다.</b> 화면에 쓸 문구를 여기 따로 적어
/// 두면 에셋을 하나 들여올 때마다 고칠 자리가 둘이 되고, 둘 중 하나는 반드시 뒤처진다.
/// 그래서 그 파일을 <c>Resources/Text/Credits.txt</c>로 옮겨 두고(<c>CreditsTextSetup</c>이
/// 맞춰 준다) 읽어서 markdown 기호만 걷어 낸다. <b>무엇을 싣고 무엇을 뺄지는 그 문서가
/// 정한다</b> — 여기서 고르지 않는다.
///
/// 만드는 쪽의 장부는 <c>docs/CREDITS.md</c>에 따로 있다. 파일 경로와 굽는 방법, 아직 출처를
/// 못 찾은 것까지 적힌 문서라 플레이어에게 흘릴 것이 아니다.
///
/// <b>왜 줄마다 Text를 만들지 않는가:</b> 두루마리는 매 프레임 통째로 움직이는데, 캔버스는
/// 자식이 움직이면 그 아래 글자를 다시 굽는다. 줄마다 하나면 200개가 매 프레임 다시 구워진다.
/// 이어지는 본문은 한 덩이로 묶어 서른 개 남짓으로 줄였다.
/// </summary>
public class CreditsScreen : FlowScreen
{
    private const int SortingOrder = 620;

    /// <summary><c>Resources</c> 기준 경로. 확장자는 붙이지 않는다.</summary>
    private const string TextPath = "Text/Credits";

    /// <summary>
    /// 글이 놓이는 폭의 최대값과 양옆 여백.
    ///
    /// 이 캔버스는 ConstantPixelSize 배율 1이라 <b>화면 픽셀이 곧 이 좌표</b>다. 창이 작으면
    /// (WebGL 기본은 960×600) 고정 폭이 화면 밖으로 삐져나가므로 실제 폭에 맞춰 줄인다.
    /// </summary>
    private const float MaxContentWidth = 1000f;
    private const float SideMargin = 48f;

    /// <summary>글자 크기 — PMD 비트맵 폰트라 모두 <b>12의 배수</b>여야 한다.</summary>
    private const int HeadingFontSize = 36;
    private const int SubFontSize = 24;
    private const int BodyFontSize = 24;

    /// <summary>초당 몇 픽셀 올라가는가. 지금 원본(3100픽셀)이 40초쯤에 지나간다.</summary>
    private const float ScrollSpeed = 80f;

    /// <summary>덩이 사이 간격. 제목 앞은 더 벌려 절이 나뉜 것이 보이게 한다.</summary>
    private const float BlockGap = 20f;
    private const float HeadingGap = 56f;

    /// <summary>두루마리가 다 지나간 뒤 잠시 비워 두는 여백. 마지막 줄이 화면 끝에서 바로 끊기지 않는다.</summary>
    private const float TailPadding = 120f;

    private static readonly Color HintColor = new Color(0.6f, 0.6f, 0.65f);

    private RectTransform content;
    private float contentWidth;
    private float contentHeight;
    private float scrolled;

    /// <summary>이미 돌아가기로 했는가. <see cref="GameFlow.GoTitle"/>이 이 화면을 지우므로 두 번 부르지 않는다.</summary>
    private bool leaving;

    public static CreditsScreen Open(GameFlow flow) =>
        Create<CreditsScreen>(flow, "CreditsScreen", SortingOrder);

    protected override void Build()
    {
        // 불투명하게 깐다. 조작 안내와 같은 밤하늘빛이다 — 뒤가 비치면 글이 읽히지 않는다.
        var backgroundGo = new GameObject("Background", typeof(RectTransform));
        backgroundGo.transform.SetParent(Root, false);
        var background = backgroundGo.AddComponent<Image>();
        background.color = new Color(0.05f, 0.07f, 0.14f);
        background.raycastTarget = false;
        PmdUi.Stretch(background.rectTransform);

        // 두루마리 몸통. 아래 끝을 축으로 잡아 두면 위로 밀어 올리는 계산이 한 줄로 끝난다.
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(Root, false);
        content = (RectTransform)contentGo.transform;
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0f);
        content.pivot = new Vector2(0.5f, 0f);

        // 창 크기는 캔버스가 세워진 뒤라야 알 수 있다. 아직 0이면 가장 넓은 값으로 둔다.
        float available = Root.rect.width - SideMargin * 2f;
        contentWidth = available > 200f ? Mathf.Min(MaxContentWidth, available) : MaxContentWidth;

        contentHeight = Layout(ReadLines());
        content.sizeDelta = new Vector2(contentWidth, contentHeight);

        // 나가는 길을 화면 왼쪽 아래에 적어 둔다. 두루마리를 끝까지 볼 사람만 있는 것이 아니다.
        Text hint = PmdUi.MakeText(Root, "Hint", "메인화면으로 돌아가려면 B 클릭", 24,
                                   TextAnchor.LowerLeft);
        hint.color = HintColor;
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0f, 0f);
        hintRect.pivot = new Vector2(0f, 0f);
        hintRect.sizeDelta = new Vector2(600f, 40f);
        hintRect.anchoredPosition = new Vector2(24f, 20f);
    }

    /// <summary>고를 칸이 없는 화면이다. 나가는 길은 B 하나뿐이다.</summary>
    protected override void Activate(int index) { }

    /// <summary>
    /// 두루마리를 밀어 올린다.
    ///
    /// <see cref="FlowScreen"/>이 이미 <c>Update</c>를 쓰고 있어 여기서는 <c>LateUpdate</c>를 쓴다
    /// (타이틀의 로고가 떠다니는 것과 같은 사정이다). 시간은 실제 시간으로 잰다 — 메뉴는
    /// <see cref="Time.timeScale"/>이 0인 채로 떠 있다.
    /// </summary>
    private void LateUpdate()
    {
        if (leaving) return;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame) { Leave(); return; }

        scrolled += Time.unscaledDeltaTime * ScrollSpeed;
        content.anchoredPosition = new Vector2(0f, scrolled - contentHeight);

        // 마지막 줄이 화면 위로 완전히 빠져나가면 끝이다. 화면 높이는 창 크기에 따라
        // 달라지므로 미리 재 두지 않고 그때그때 묻는다.
        if (scrolled >= contentHeight + Root.rect.height + TailPadding) Leave();
    }

    private void Leave()
    {
        leaving = true;
        Flow.GoTitle();
    }

    // ---------------------------------------------------------------- 배치

    /// <summary>
    /// 줄들을 위에서부터 쌓고 전체 높이를 돌려준다.
    ///
    /// 이어지는 본문은 한 Text에 <c>\n</c>으로 몰아 넣는다. 줄바꿈된 실제 높이는
    /// <see cref="Text.preferredHeight"/>가 알려 주는데, <b>폭을 먼저 정해야</b> 답이 맞는다.
    /// </summary>
    private float Layout(List<Line> lines)
    {
        float y = 0f;
        var body = new StringBuilder();

        // 쌓인 이름들을 한 덩이로 내려놓는다. 원본이 한 줄에 하나씩 적어 둔 것이라
        // 줄바꿈을 그대로 지킨다 — 이어 붙이면 이름 목록이 한 문단이 되어 버린다.
        void FlushBody()
        {
            if (body.Length == 0) return;
            y += AddBlock(body.ToString(), BodyFontSize, PmdUi.TextColor, y, TextAnchor.UpperCenter);
            y += BlockGap;
            body.Clear();
        }

        foreach (Line line in lines)
        {
            switch (line.kind)
            {
                case Kind.Blank:
                    FlushBody();
                    break;

                case Kind.Heading:
                    FlushBody();
                    y += HeadingGap;
                    y += AddBlock(line.text, HeadingFontSize, PmdUi.AccentColor, y, TextAnchor.UpperCenter);
                    y += BlockGap;
                    break;

                case Kind.Sub:
                    FlushBody();
                    y += AddBlock(line.text, SubFontSize, PmdUi.HighlightColor, y, TextAnchor.UpperCenter);
                    y += BlockGap;
                    break;

                case Kind.Note:
                    FlushBody();
                    y += AddBlock(line.text, BodyFontSize, PmdUi.DisabledColor, y, TextAnchor.UpperCenter);
                    y += BlockGap;
                    break;

                default:
                    if (body.Length > 0) body.Append('\n');
                    body.Append(line.text);
                    break;
            }
        }
        FlushBody();
        return y;
    }

    /// <summary>글 덩이 하나를 <paramref name="top"/> 아래에 놓고 그 높이를 돌려준다.</summary>
    private float AddBlock(string body, int size, Color color, float top, TextAnchor anchor)
    {
        Text text = PmdUi.MakeText(content, "Block", body, size, anchor);
        text.color = color;

        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        // 폭을 먼저 못박아야 preferredHeight가 줄바꿈까지 셈에 넣는다.
        rt.sizeDelta = new Vector2(contentWidth, size);

        float height = Mathf.Max(text.preferredHeight, size);
        rt.sizeDelta = new Vector2(contentWidth, height);
        rt.anchoredPosition = new Vector2(0f, -top);
        return height;
    }

    // ---------------------------------------------------------------- 원본 읽기

    /// <summary>
    /// 줄의 종류.
    ///
    /// <see cref="Note"/>는 절 밑에 붙는 <i>기울인 한 줄</i>("출처: …")이다. 이름들과 같은
    /// 색으로 두면 그것도 크레딧에 오른 이름처럼 읽혀서, 흐린 색으로 한 단 낮춘다.
    /// </summary>
    private enum Kind { Heading, Sub, Note, Body, Blank }

    private struct Line
    {
        public string text;
        public Kind kind;
        public Line(string text, Kind kind) { this.text = text; this.kind = kind; }
    }

    private static List<Line> ReadLines()
    {
        var asset = Resources.Load<TextAsset>(TextPath);
        if (asset == null)
        {
            // 조용히 빈 화면을 띄우지 않는다. 파일이 빠졌다는 것 자체가 알려야 할 일이다.
            Debug.LogWarning("크레딧 원본을 찾지 못했다: Resources/" + TextPath
                             + " (CreditsTextSetup으로 docs/CREDITS.md에서 갱신한다)");
            return new List<Line> { new Line("크레딧 원본을 찾지 못했습니다.", Kind.Body) };
        }
        return Parse(asset.text);
    }

    /// <summary>
    /// markdown 기호를 걷어 내고 줄마다 격을 매긴다. 원본(<c>docs/FINALCREDITS.md</c>)이 쓰는
    /// 형식은 넷뿐이다.
    ///
    /// <list type="bullet">
    /// <item><c>[ 절 이름 ]</c> — 큰 제목. <c>###</c>가 앞에 붙기도 하고 안 붙기도 하는데,
    ///   대괄호로 감싼 줄이면 어느 쪽이든 제목으로 본다</item>
    /// <item><c>**이름**</c> 한 줄 — 아래 목록이 누구 것인지 알리는 작은 제목</item>
    /// <item><c>*기울인 한 줄*</c> — 출처를 밝히는 곁말</item>
    /// <item><c>- 항목</c>과 그냥 한 줄 — 이름</item>
    /// </list>
    /// </summary>
    private static List<Line> Parse(string raw)
    {
        string[] source = raw.Replace("\r\n", "\n").Split('\n');
        var lines = new List<Line>();

        foreach (string rawLine in source)
        {
            string line = rawLine.Trim();

            if (line.Length == 0) { AddBlank(lines); continue; }

            // 인용문(>)은 문서를 고치는 사람에게 남긴 메모다. 플레이어가 볼 것이 아니다.
            if (line.StartsWith(">")) continue;

            // 가로줄은 절 구분이라 빈 줄로 바꾼다.
            if (line.StartsWith("---")) { AddBlank(lines); continue; }

            line = line.TrimStart('#').TrimStart();
            if (line.Length == 0) continue;

            // 대괄호로 감싼 줄이 절 제목이다.
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                lines.Add(new Line(Clean(line), Kind.Heading));
                continue;
            }

            bool bold = line.StartsWith("**") && line.EndsWith("**") && line.Length > 4;
            bool italic = !bold && line.StartsWith("*") && line.EndsWith("*") && line.Length > 2;

            if (line.StartsWith("- ") || line.StartsWith("* ")) line = line.Substring(2);

            lines.Add(new Line(Clean(line), bold ? Kind.Sub : italic ? Kind.Note : Kind.Body));
        }
        return lines;
    }

    /// <summary>빈 줄은 이어지지 않게 하나로 눌러 둔다. 원본의 문단 사이가 화면에서는 큰 여백이 된다.</summary>
    private static void AddBlank(List<Line> lines)
    {
        if (lines.Count == 0 || lines[lines.Count - 1].kind == Kind.Blank) return;
        lines.Add(new Line("", Kind.Blank));
    }

    /// <summary>강조·코드 표시를 걷고, 링크는 "글 (주소)"로 편다 — 주소도 출처의 일부다.</summary>
    private static string Clean(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // 강조 별표는 한 개짜리(기울임)도 걷는다. 지금 원본에 별표를 글자로 쓰는 곳은 없다.
            if (c == '*') continue;
            if (c == '`') continue;

            if (c == '[')
            {
                int close = text.IndexOf(']', i);
                int open = close >= 0 && close + 1 < text.Length && text[close + 1] == '('
                    ? close + 1 : -1;
                int end = open >= 0 ? text.IndexOf(')', open) : -1;
                if (end > 0)
                {
                    sb.Append(text, i + 1, close - i - 1)
                      .Append(" (").Append(text, open + 1, end - open - 1).Append(')');
                    i = end;
                    continue;
                }
            }

            sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
