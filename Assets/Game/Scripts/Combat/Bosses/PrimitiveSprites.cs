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
