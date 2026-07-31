using UnityEngine;

/// <summary>
/// 창을 풀HD(1920×1080) 하나로 고정한다.
///
/// UI가 전부 코드로 픽셀 단위 배치되어 있어 해상도가 달라지면 검증하지 않은 배치로
/// 뜬다. 빌드 설정에서 기본 크기와 크기 조절 금지를 걸어 두지만, 유니티는 마지막으로
/// 쓴 창 크기를 PlayerPrefs에 기억해 두었다가 그걸로 열기 때문에 — 과거에 다른 크기로
/// 실행한 적이 있으면 설정값이 무시된다. 그래서 실행 시점에 한 번 더 못박는다.
///
/// 에디터에서는 게임 뷰 크기를 마음대로 봐야 하므로 손대지 않는다.
/// </summary>
public static class FixedResolution
{
    public const int Width = 1920;
    public const int Height = 1080;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        if (Application.isEditor) return;
        // 웹에서는 페이지(index.html)가 캔버스 크기를 정한다 — 창에 맞춰 16:9로 줄어드는
        // 캔버스에 여기서 1920을 다시 박으면 둘이 서로 덮어쓰며 싸운다.
        if (Application.platform == RuntimePlatform.WebGLPlayer) return;
        if (Screen.width == Width && Screen.height == Height &&
            Screen.fullScreenMode == FullScreenMode.Windowed) return;
        Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
    }
}
