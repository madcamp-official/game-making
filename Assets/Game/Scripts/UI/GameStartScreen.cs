using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 시작 화면. 게임을 정지 상태로 열고, 클릭 또는 아무 키 입력으로 시작한다.
/// UI는 런타임에 코드로 구성해 별도 씬 없이 동작한다.
/// </summary>
public class GameStartScreen : MonoBehaviour
{
    [SerializeField] private string gameTitle = "이상해씨의 던전 탐험";
    [SerializeField] private string subtitle = "포켓몬 로그라이트 프로토타입";
    [SerializeField, TextArea] private string controlsText =
        "이동 WASD   ·   조준 마우스\n기본 공격 좌클릭   ·   잎날가르기 우클릭\n상호작용 E   ·   재시작 R";
    [SerializeField, TextArea] private string creditsText =
        "Sprites: PMD Sprite Repository (PMDCollab/SpriteCollab) — (C) CHUNSOFT / Pokemon\n비상업 팬 프로젝트";

    private GameObject panel;

    private void Start()
    {
        BuildPanel();
        Time.timeScale = 0f;
    }

    private void Update()
    {
        if (panel == null) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool pressed = (kb != null && kb.anyKey.wasPressedThisFrame) ||
                       (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (!pressed) return;

        Time.timeScale = 1f;
        Destroy(panel);
        panel = null;
        enabled = false;
    }

    private void BuildPanel()
    {
        Font font = PixelUi.Font;

        panel = new GameObject("StartScreen");
        panel.transform.SetParent(transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.05f, 0.08f, 0.93f);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        MakeText("Title", gameTitle, font, 84, new Vector2(0, 170), new Color(0.55f, 0.95f, 0.55f));
        MakeText("Subtitle", subtitle, font, 24, new Vector2(0, 95), new Color(0.8f, 0.85f, 0.8f));
        MakeText("Controls", controlsText, font, 24, new Vector2(0, -60), Color.white);
        MakeText("Prompt", "클릭 또는 아무 키나 눌러 시작", font, 36, new Vector2(0, -220), new Color(1f, 0.85f, 0.3f));
        // 크레딧은 화면 아래에 붙인다. 캔버스가 정수 배율(Constant Pixel Size)이라
        // 창이 작으면 중앙 기준 고정 오프셋으로는 화면 밖으로 밀려난다.
        Text credits = MakeText("Credits", creditsText, font, 24, Vector2.zero, new Color(0.6f, 0.6f, 0.65f));
        RectTransform creditsRt = credits.rectTransform;
        creditsRt.anchorMin = creditsRt.anchorMax = new Vector2(0.5f, 0f);
        creditsRt.anchoredPosition = new Vector2(0, 60);
    }

    private Text MakeText(string name, string content, Font font, int size, Vector2 anchoredPos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(panel.transform, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = PixelUi.SnapFontSize(size);   // 비트맵 폰트라 12의 배수여야 한다
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(1600, 100);
        return text;
    }
}
