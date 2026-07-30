using UnityEditor;
using UnityEngine;

/// <summary>
/// 코드로 만드는 UI가 쓰는 9슬라이스 스프라이트를 들여온다.
///
/// 모양은 <c>scratchpad/bake_ui.py</c>가 굽고, 여기서는 <b>테두리 두께</b>를 정해 준다.
/// 두께는 그림 안에 적어 둘 수 없는 값이라(픽셀만 보고는 어디까지가 모서리인지 알 수 없다)
/// 굽는 쪽과 여기 둘 다 알고 있어야 한다 — 두 값이 어긋나면 창을 늘릴 때 모서리가 늘어진다.
///
/// ⚠️ <b>PPU는 캔버스의 referencePixelsPerUnit(100)과 같아야 한다.</b> 9슬라이스 테두리는
/// <c>sprite.border / (spritePPU / 100 * pixelsPerUnitMultiplier)</c>만큼 그려지므로, PPU를 1로
/// 두면 테두리가 100배로 부풀어 모서리가 창을 다 먹는다. 예전에 이 값이 1이었고, 화면마다
/// 테두리가 통째로 뭉개져 보인 원인이 이것이었다. 100으로 두면 스프라이트 픽셀이 UI 단위와
/// 1:1이라, 폰트 24(기본 12의 두 배)에 맞춰 2배로 구운 원본이 의도한 크기로 그려진다.
/// </summary>
public static class UiSpriteSetup
{
    private const string Folder = "Assets/Game/Resources/UI/";

    /// <summary>uGUI 캔버스의 기본 referencePixelsPerUnit. 이 값과 맞춰야 테두리가 1:1이 된다.</summary>
    private const int PixelsPerUnit = 100;

    /// <summary>(파일 이름, 9슬라이스 테두리). 테두리는 (좌, 하, 우, 상) 순이다.</summary>
    private static readonly (string name, Vector4 border)[] Sprites =
    {
        // 대화창 — 좌우가 두껍고 위아래가 얇은 비대칭이다. textbox.png가 그렇게 생겼다.
        ("PmdPanel", new Vector4(14, 6, 14, 6)),
        // 버튼 — 세로 테두리를 크게 잡아 밝은 적색 띠가 늘어나지 않게 한다.
        ("PmdButton", new Vector4(10, 14, 10, 14)),
        ("PmdButtonOn", new Vector4(10, 14, 10, 14)),
        ("PmdButtonOff", new Vector4(10, 14, 10, 14)),
        // 기술 칸 테두리 — 가운데가 비어 있어 안쪽 띠가 보인다.
        ("PmdMoveFrame", new Vector4(6, 6, 6, 6)),
        ("PmdMoveFrameOff", new Vector4(6, 6, 6, 6)),
        // 체력바 틀 — 2px 윤곽(2배라 4px). 얇게 두면 밝은 지형 위에서 바의 경계가 풀린다.
        ("PmdBarFrame", new Vector4(4, 4, 4, 4)),
        // 작은 꼬리표는 1px 윤곽(2배라 2px)이다. 여기까지 두껍게 하면 안쪽에 글자칸이 안 남는다.
        ("PmdChip", new Vector4(2, 2, 2, 2)),
        // 타이틀 로고 — 늘려 쓰지 않으므로 테두리가 없다. logo.png의 흰 배경을 걷어낸 것이다
        // (scratchpad/bake_logo.py). PPU가 100이라 그림 픽셀이 화면 픽셀과 1:1이다.
        ("PmdLogo", Vector4.zero),
    };

    public static string ImportAll()
    {
        int done = 0;
        string missing = "";

        foreach ((string name, Vector4 border) in Sprites)
        {
            string path = Folder + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { missing += " " + name; continue; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            // spriteBorder는 TextureImporterSettings를 거쳐야 저장된다.
            // importer.spriteBorder 직접 대입은 조용히 무시된다.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = border;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
            done++;
        }

        return "UI 스프라이트 " + done + "장 들여옴"
             + (missing.Length > 0 ? " (없음:" + missing + " — bake_ui.py를 먼저)" : "");
    }
}
