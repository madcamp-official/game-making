using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

/// <summary>
/// itch.io용 WebGL 빌드. 플랫폼 전환까지 <see cref="BuildPipeline.BuildPlayer"/>가 알아서 한다.
///
/// 빌드는 에디터를 몇 분씩 붙잡아 밖에서 상태를 물어볼 수 없다. 그래서 시작할 때
/// BUILDING, 끝날 때 결과를 <c>Builds/webgl_build_log.txt</c>에 적는다 — 지켜보는 쪽은
/// 이 파일만 읽으면 된다.
/// </summary>
public static class WebGLBuilder
{
    private const string OutputDir = "Builds/WebGL";
    private const string LogPath = "Builds/webgl_build_log.txt";

    [MenuItem("Tools/WebGL 빌드 (itch.io)")]
    public static void Build()
    {
        Directory.CreateDirectory("Builds");
        File.WriteAllText(LogPath, "BUILDING\n");
        try
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.WebGL,
                locationPathName = OutputDir,
                options = BuildOptions.None,
            };
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            PatchIndexHtml();
            File.WriteAllText(LogPath,
                summary.result + "\n" +
                "총 크기: " + (summary.totalSize / (1024 * 1024)) + "MB\n" +
                "걸린 시간: " + summary.totalTime + "\n" +
                "에러 " + summary.totalErrors + " / 경고 " + summary.totalWarnings + "\n");
        }
        catch (System.Exception e)
        {
            File.WriteAllText(LogPath, "EXCEPTION\n" + e + "\n");
            throw;
        }
    }

    /// <summary>
    /// 빌드된 index.html에 두 가지를 덧댄다. 패치마다 표식을 확인하므로 몇 번을 다시
    /// 돌려도 두 번 덧대지지 않는다 — 빌드 없이 패치만 다시 적용할 수도 있다 (MCP에서 호출).
    ///
    /// <b>1) 창 맞춤.</b> 기본 템플릿은 데스크톱에서 캔버스를 1920px로 고정한다 — 창이
    /// 그보다 작으면(itch.io 임베드, 노트북 화면) 왼쪽 위 귀퉁이만 보이고 스크롤바가 생긴다.
    /// 창에 맞춰 16:9를 지키며 줄어들게 바꾼다. 렌더 해상도는 엔진이 캔버스 표시 크기에
    /// 맞춰 따라간다 (matchWebGLToCanvasSize 기본값).
    ///
    /// <b>2) 에임 커서.</b> Unity WebGL의 Cursor.SetCursor는 브라우저에 안 먹고 inline
    /// cursor:default만 남는다. 그래서 GameCursor와 같은 하얀 십자를 페이지 쪽에서
    /// 캔버스 CSS 커서로 그린다 — !important라 Unity가 박은 inline 값을 이긴다.
    /// </summary>
    public static void PatchIndexHtml()
    {
        string path = Path.Combine(OutputDir, "index.html");
        string html = File.ReadAllText(path);

        if (!html.Contains("fitCanvas"))
        {
            const string OldWidth = "canvas.style.width = \"1920px\";";
            const string OldHeight = "canvas.style.height = \"1080px\";";
            const string Fit = @"function fitCanvas() {
          var scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
          canvas.style.width = Math.round(1920 * scale) + ""px"";
          canvas.style.height = Math.round(1080 * scale) + ""px"";
        }
        fitCanvas();
        window.addEventListener('resize', fitCanvas);";

            if (!html.Contains(OldWidth) || !html.Contains(OldHeight))
                throw new System.InvalidOperationException(
                    "index.html에서 고정 크기 코드를 찾지 못했다 — 템플릿이 바뀌었는지 확인할 것: " + path);

            html = html.Replace(OldWidth, Fit).Replace(OldHeight, "");
        }

        if (!html.Contains("pokebrawl-cursor"))
        {
            // GameCursor.Build와 같은 그림이다: 16칸 격자 × 4배 확대, 중심 점 + 2~5칸 팔,
            // 십자 픽셀마다 3×3 어두운 블록을 먼저 깔아 테두리를 만든다.
            const string CursorScript = @"
      // pokebrawl-cursor: 게임 내 GameCursor와 같은 하얀 픽셀 십자 에임.
      (function () {
        var cell = 4, grid = 16, c = 7;
        var cur = document.createElement('canvas');
        cur.width = cur.height = grid * cell;
        var ctx = cur.getContext('2d');
        var dots = [[c, c]];
        for (var o = 2; o <= 5; o++) dots.push([c+o, c], [c-o, c], [c, c+o], [c, c-o]);
        ctx.fillStyle = '#191919';
        dots.forEach(function (p) { ctx.fillRect((p[0]-1)*cell, (p[1]-1)*cell, cell*3, cell*3); });
        ctx.fillStyle = '#f5f5f5';
        dots.forEach(function (p) { ctx.fillRect(p[0]*cell, p[1]*cell, cell, cell); });
        var hot = c * cell + cell / 2;
        var style = document.createElement('style');
        style.textContent = '#unity-canvas { cursor: url(' + cur.toDataURL() +
                            ') ' + hot + ' ' + hot + ', crosshair !important; }';
        document.head.appendChild(style);
      })();
";
            const string Anchor = "var canvas = document.querySelector(\"#unity-canvas\");";
            if (!html.Contains(Anchor))
                throw new System.InvalidOperationException(
                    "index.html에서 캔버스 선언을 찾지 못했다: " + path);
            html = html.Replace(Anchor, Anchor + CursorScript);
        }

        File.WriteAllText(path, html);
    }
}
