using UnityEngine;

/// <summary>
/// OS 마우스 커서를 하얀 픽셀 십자 에임으로 바꾼다.
///
/// 기본 화살표는 바닥 타일 위에서 눈에 잘 띄지 않는데, 이 게임은 네 기술이 전부
/// 마우스 방향 조준이라 커서를 놓치는 순간 조준을 놓친다. 에임 십자는 "지금 여기를
/// 겨누고 있다"를 화살표보다 훨씬 또렷하게 말해 준다.
///
/// 씬에 붙지 않고 스스로 걸린다 — 타이틀·메뉴·게임 어디서나 같은 커서를 쓰므로
/// 씬별로 챙길 이유가 없다.
/// </summary>
public static class GameCursor
{
    /// <summary>논리 픽셀 하나를 화면에서 이만큼 키운다. 이 확대가 곧 픽셀화된 느낌이다.</summary>
    private const int Cell = 2;
    /// <summary>논리 격자 크기. ×Cell = 32픽셀로, 하드웨어 커서가 지원하는 안전한 상한이다.</summary>
    private const int Grid = 16;
    /// <summary>십자 중심의 논리 좌표.</summary>
    private const int Center = 7;

    private static readonly Color32 Body = new Color32(245, 245, 245, 255);
    /// <summary>하양 둘레의 어두운 테두리. 밝은 바닥에서도, 붉은 예고 위에서도 형태가 남는다.</summary>
    private static readonly Color32 Outline = new Color32(25, 25, 25, 255);

    private static Texture2D texture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Apply()
    {
        if (texture == null) texture = Build();
        // 십자 중심 = 논리 (7,7)이 차지하는 2×2 블록의 한가운데. 핫스팟은 왼쪽 위 원점이다.
        Cursor.SetCursor(texture, new Vector2(Center * Cell + 1, Center * Cell + 1), CursorMode.Auto);
        Cursor.visible = true;
    }

    private static Texture2D Build()
    {
        // 십자 모양: 중심 점 하나 + 상하좌우 팔. 팔은 중심에서 2~5칸 — 중심 바로 옆
        // 1칸을 비워 두어, 겨누는 지점 자체는 십자에 가리지 않고 보인다.
        bool[,] body = new bool[Grid, Grid];
        body[Center, Center] = true;
        for (int offset = 2; offset <= 5; offset++)
        {
            body[Center + offset, Center] = true;
            body[Center - offset, Center] = true;
            body[Center, Center + offset] = true;
            body[Center, Center - offset] = true;
        }

        // 십자 픽셀의 8방향 이웃을 테두리색으로 두른다.
        bool[,] outline = new bool[Grid, Grid];
        for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
            {
                if (body[x, y]) continue;
                for (int dy = -1; dy <= 1 && !outline[x, y]; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Grid || ny >= Grid || !body[nx, ny]) continue;
                        outline[x, y] = true;
                        break;
                    }
            }

        var tex = new Texture2D(Grid * Cell, Grid * Cell, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            // 플레이 모드를 들락여도 다시 만들지 않도록 씬 언로드에서 살아남는다.
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Color32[Grid * Cell * Grid * Cell];
        var clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < Grid * Cell; y++)
            for (int x = 0; x < Grid * Cell; x++)
            {
                int gx = x / Cell;
                // 텍스처는 아래가 y=0이지만 핫스팟·격자는 위가 원점이라 뒤집어 읽는다.
                int gy = Grid - 1 - y / Cell;
                pixels[y * Grid * Cell + x] =
                    body[gx, gy] ? Body : outline[gx, gy] ? Outline : clear;
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
