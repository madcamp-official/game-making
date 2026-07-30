using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 공격 연출에 쓰는 단색 도형 스프라이트.
///
/// 공격마다 텍스처를 새로 만들면 금방 쌓이므로 한 번 만들어 두고 계속 재사용한다.
/// 파괴된 뒤(플레이 모드 재진입 등)에는 널 검사에 걸려 다시 만들어진다.
/// </summary>
public static class PrimitiveSprites
{
    private const int Resolution = 64;
    private const float RingInnerRatio = 0.78f;

    private static Sprite square;
    private static Sprite circle;
    private static Sprite ring;
    private static Sprite triangle;
    // 부채꼴은 각도마다 모양이 달라서 각도별로 하나씩 만들어 재사용한다.
    private static readonly Dictionary<int, Sprite> sectors = new Dictionary<int, Sprite>();

    /// <summary>1×1유닛 흰색 사각형. 크기는 localScale로 정한다.</summary>
    public static Sprite Square
    {
        get
        {
            if (square == null) square = MakeSquare();
            return square;
        }
    }

    /// <summary>지름 1유닛의 채워진 흰 원.</summary>
    public static Sprite Circle
    {
        get
        {
            if (circle == null) circle = MakeCircle(0f);
            return circle;
        }
    }

    /// <summary>지름 1유닛의 흰 테두리 원.</summary>
    public static Sprite Ring
    {
        get
        {
            if (ring == null) ring = MakeCircle(RingInnerRatio);
            return ring;
        }
    }

    /// <summary>
    /// 1×1유닛의 채워진 삼각형. 밑변이 -X 끝, 꼭짓점이 +X 끝이다.
    /// localScale을 (길이, 밑변 너비)로 주면 창끝 모양이 된다 — 코뿌리의 뿔드릴이 쓴다.
    /// </summary>
    public static Sprite Triangle
    {
        get
        {
            if (triangle == null) triangle = MakeTriangle();
            return triangle;
        }
    }

    private static Sprite MakeTriangle()
    {
        Texture2D tex = new Texture2D(Resolution, Resolution) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[Resolution * Resolution];

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                // u: 밑변(0)에서 꼭짓점(1)까지, v: 중심선에서의 거리
                float u = (x + 0.5f) / Resolution;
                float v = Mathf.Abs((y + 0.5f) / Resolution - 0.5f);
                // 꼭짓점으로 갈수록 폭이 0으로 좁아진다.
                float halfWidth = 0.5f * (1f - u);
                // 가장자리 1픽셀을 부드럽게 깎아 계단을 줄인다.
                float alpha = Mathf.Clamp01((halfWidth - v) * Resolution);
                pixels[y * Resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Resolution, Resolution),
                                      new Vector2(0.5f, 0.5f), Resolution);
        sprite.name = "Triangle";
        return sprite;
    }

    /// <summary>
    /// 지름 1유닛의 채워진 부채꼴. +X 방향을 중심으로 좌우 <paramref name="sweepDegrees"/>/2씩 벌어진다.
    /// 방향은 transform 회전으로 맞춘다.
    /// </summary>
    public static Sprite Sector(float sweepDegrees)
    {
        // 1도 단위로 캐시한다. 은빛바람은 안전 부채꼴과 그 여집합인 위험 부채꼴을 맞붙여 그리는데,
        // 각도를 뭉뚱그리면 경계가 실제 탄 궤적과 어긋나 예고가 거짓말이 된다.
        // 실제로 쓰는 각도는 페이즈당 두 종류뿐이라 캐시가 커질 일은 없다.
        int key = Mathf.Clamp(Mathf.RoundToInt(sweepDegrees), 1, 360);
        if (!sectors.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            sprite = MakeSector(key);
            sectors[key] = sprite;
        }
        return sprite;
    }

    private static Sprite MakeSector(float sweepDegrees)
    {
        Texture2D tex = new Texture2D(Resolution, Resolution) { filterMode = FilterMode.Bilinear };
        float radius = Resolution * 0.5f;
        float halfSweep = sweepDegrees * 0.5f;
        Color[] pixels = new Color[Resolution * Resolution];

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - distance);

                // +X에서 벌어진 각도. 부채꼴 밖이면 투명하게 둔다.
                float angle = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                if (angle > halfSweep) alpha = 0f;

                pixels[y * Resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Resolution, Resolution), new Vector2(0.5f, 0.5f), Resolution);
        // 이름을 붙여 두면 계층 창과 디버그에서 어떤 도형인지 바로 보인다.
        sprite.name = "Sector" + Mathf.RoundToInt(sweepDegrees);
        return sprite;
    }

    /// <summary>
    /// 돌진 예고 한 장 — <b>지나갈 복도와 맞는 부채꼴을 한 도형으로 합쳐서</b> 굽는다.
    ///
    /// 둘을 따로 그리면 겹친 자리에서 알파가 두 번 쌓여 그 부분만 진해진다. 한 번의 공격을
    /// 그린 것인데 경계선이 생겨 셋으로 나뉘어 보였다 — 복도, 부채꼴, 그리고 둘이 겹친 띠.
    /// 여기서는 두 모양의 <b>합집합</b>을 한 텍스처에 굽고 겹친 자리는 진한 쪽을 취하므로,
    /// 이음매 없이 하나의 덩어리로 읽힌다.
    ///
    /// 그러면서도 "지나갈 자리"와 "실제로 맞는 자리"의 구분은 살아 있다.
    /// <paramref name="corridorWeight"/>가 복도 쪽 알파를 낮춰 두기 때문이다 — 색을 두 번
    /// 칠하는 대신 텍스처 안에 진하기 차이를 새겨 둔다.
    ///
    /// 좌표는 <b>월드 단위 그대로</b>다. 피벗이 몸 중심(x=0)에 놓이므로 부르는 쪽은
    /// localScale을 건드리지 말고 자리와 회전만 맞추면 된다.
    /// </summary>
    /// <param name="corridorLength">몸 중심에서 돌진이 끝나는 곳까지.</param>
    /// <param name="corridorHalfWidth">복도 반너비 (몸통 굵기의 절반).</param>
    /// <param name="hitCenter">타격 순간 몸이 있을 자리까지의 거리.</param>
    /// <param name="hitRadius">그 자리에서의 판정 반지름.</param>
    /// <param name="corridorWeight">복도 알파를 부채꼴 대비 몇 할로 둘지 (0~1).</param>
    public static Sprite DashZone(float corridorLength, float corridorHalfWidth,
                                  float hitCenter, float hitRadius, float sweepDegrees,
                                  float corridorWeight)
    {
        var key = (Q(corridorLength), Q(corridorHalfWidth), Q(hitCenter), Q(hitRadius),
                   Mathf.Clamp(Mathf.RoundToInt(sweepDegrees), 1, 360),
                   Mathf.RoundToInt(Mathf.Clamp01(corridorWeight) * 20f));

        if (!dashZones.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            sprite = MakeDashZone(corridorLength, corridorHalfWidth, hitCenter, hitRadius,
                                  sweepDegrees, Mathf.Clamp01(corridorWeight));
            dashZones[key] = sprite;
        }
        return sprite;
    }

    /// <summary>0.02유닛 단위로 끊어 캐시 열쇠를 만든다. 몸 반지름은 콜라이더에서 재는 값이라
    /// 프레임마다 미세하게 흔들릴 수 있는데, 그때마다 텍스처를 새로 구우면 안 된다.</summary>
    private static int Q(float value) => Mathf.RoundToInt(value * 50f);

    private static readonly Dictionary<(int, int, int, int, int, int), Sprite> dashZones =
        new Dictionary<(int, int, int, int, int, int), Sprite>();

    /// <summary>돌진 예고의 픽셀 밀도. 예고는 큼직한 단색 덩어리라 촘촘할 이유가 없다.</summary>
    private const float DashZonePixelsPerUnit = 32f;

    /// <summary>한 변의 픽셀 상한. 값이 잘못 들어와도 거대한 텍스처를 굽지 않게 막는다.</summary>
    private const int DashZoneMaxPixels = 512;

    private static Sprite MakeDashZone(float corridorLength, float corridorHalfWidth,
                                       float hitCenter, float hitRadius, float sweepDegrees,
                                       float corridorWeight)
    {
        const float ppu = DashZonePixelsPerUnit;
        float halfSweep = sweepDegrees * 0.5f;

        // 두 모양을 모두 담는 사각형. 부채꼴이 몸 뒤로 벌어질 수 있어 왼쪽도 열어 둔다.
        float minX = Mathf.Min(0f, hitCenter - hitRadius);
        float maxX = Mathf.Max(corridorLength, hitCenter + hitRadius);
        float halfY = Mathf.Max(corridorHalfWidth, hitRadius);

        int width = Mathf.Clamp(Mathf.CeilToInt((maxX - minX) * ppu), 1, DashZoneMaxPixels);
        int height = Mathf.Clamp(Mathf.CeilToInt(halfY * 2f * ppu), 1, DashZoneMaxPixels);

        var tex = new Texture2D(width, height) { filterMode = FilterMode.Bilinear };
        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float wy = -halfY + (y + 0.5f) / ppu;
            for (int x = 0; x < width; x++)
            {
                float wx = minX + (x + 0.5f) / ppu;

                // --- 복도: 몸 중심에서 앞으로 뻗은 띠
                float corridor = 0f;
                if (wx >= 0f && corridorLength > 0f && corridorHalfWidth > 0f)
                {
                    float endEdge = Mathf.Clamp01((corridorLength - wx) * ppu);
                    float sideEdge = Mathf.Clamp01((corridorHalfWidth - Mathf.Abs(wy)) * ppu);
                    corridor = Mathf.Min(endEdge, sideEdge);
                }

                // --- 부채꼴: 타격 순간의 자리에서 앞쪽으로 벌어진 판정 범위
                float dx = wx - hitCenter;
                float distance = Mathf.Sqrt(dx * dx + wy * wy);
                float radiusEdge = Mathf.Clamp01((hitRadius - distance) * ppu);
                // 각도 경계도 호의 길이로 환산해 깎는다. 중심에 가까울수록 한 도가 짧아지므로
                // 각도를 그대로 쓰면 안쪽만 과하게 깎여 부채꼴 꼭지가 뾰족하게 파인다.
                float angle = Mathf.Abs(Mathf.Atan2(wy, dx) * Mathf.Rad2Deg);
                float angleEdge = distance < 1f / ppu
                    ? 1f
                    : Mathf.Clamp01((halfSweep - angle) * Mathf.Deg2Rad * distance * ppu);
                float sector = Mathf.Min(radiusEdge, angleEdge);

                // 합집합 — 겹친 자리는 진한 쪽만 남는다. 더하면 그 띠만 두 배로 짙어진다.
                float alpha = Mathf.Max(corridor * corridorWeight, sector);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        // 피벗을 몸 중심(x=0)에 놓는다. 부르는 쪽이 자리 계산을 하지 않게 하려는 것이다.
        var pivot = new Vector2(maxX - minX <= 0f ? 0.5f : (0f - minX) / (maxX - minX), 0.5f);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), pivot, ppu);
        sprite.name = "DashZone";
        return sprite;
    }

    private static Sprite MakeSquare()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "Square";
        return sprite;
    }

    /// <param name="innerRatio">0이면 꽉 찬 원, 0보다 크면 그 비율 안쪽이 비어 테두리만 남는다.</param>
    private static Sprite MakeCircle(float innerRatio)
    {
        Texture2D tex = new Texture2D(Resolution, Resolution) { filterMode = FilterMode.Bilinear };
        float radius = Resolution * 0.5f;
        float inner = radius * innerRatio;
        Color[] pixels = new Color[Resolution * Resolution];

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                // 가장자리 1픽셀을 부드럽게 깎아 계단을 줄인다.
                float alpha = Mathf.Clamp01(radius - distance);
                if (inner > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - inner));
                pixels[y * Resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Resolution, Resolution), new Vector2(0.5f, 0.5f), Resolution);
        sprite.name = innerRatio > 0f ? "Ring" : "Circle";
        return sprite;
    }
}
