using UnityEngine;

/// <summary>
/// 픽셀 UI 공통 규칙. PMD 비트맵 폰트(`Resources/Fonts/PMDFont`)를 코드에서 만드는 UI에도 쓰기 위한 접근자와,
/// 글자가 뭉개지지 않게 하는 크기 규칙을 한곳에 모아 둔다.
///
/// 폰트가 비트맵이라 확대 배율이 정수가 아니면 획 굵기가 들쭉날쭉해진다. 그래서
/// 폰트 크기는 반드시 <see cref="BaseFontSize"/>(12)의 배수여야 하고, 캔버스 배율도 정수여야 한다.
/// </summary>
public static class PixelUi
{
    /// <summary>PMDFont의 기준 크기. 이 값의 배수로만 폰트 크기를 정해야 픽셀이 깨지지 않는다.</summary>
    public const int BaseFontSize = 12;

    /// <summary>캔버스 배율 1단계에 해당하는 화면 높이. 1080 미만에서는 항상 1배.</summary>
    public const int ReferenceHeight = 1080;

    private static Font font;
    private static Material worldMaterial;

    /// <summary>UI Text용 PMD 비트맵 폰트. 없으면 내장 폰트로 대체한다.</summary>
    public static Font Font
    {
        get
        {
            if (font == null)
            {
                font = Resources.Load<Font>("Fonts/PMDFont");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return font;
        }
    }

    /// <summary>월드 공간 TextMesh용 머티리얼. UI/Default는 캔버스 밖에서 쓰기 부적절하다.</summary>
    public static Material WorldFontMaterial
    {
        get
        {
            if (worldMaterial == null) worldMaterial = Resources.Load<Material>("Fonts/PMDFont_World");
            return worldMaterial;
        }
    }

    /// <summary>현재 화면에서 쓸 정수 캔버스 배율.</summary>
    public static int PixelScale => Mathf.Max(1, Screen.height / ReferenceHeight);

    /// <summary>가장 가까운 <see cref="BaseFontSize"/> 배수로 맞춘다 (최소 1배).</summary>
    public static int SnapFontSize(int size) =>
        Mathf.Max(BaseFontSize, Mathf.RoundToInt(size / (float)BaseFontSize) * BaseFontSize);
}
