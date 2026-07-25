using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 풀스크린 진화 컷씬. KleinStudio의 Pokémon Essentials Evolution Scene(V1.1, Ruby)을
/// 이 게임에 맞게 이식했다 (원본: Scripts/Evolution/Script.txt).
///
/// 연출 순서 (원본과 동일):
///  1. 배경과 현재 모습 등장, "진화하려고 한다!" 메시지
///  2. 현재 모습이 흰 실루엣으로 변함
///  3. 두 실루엣이 점점 빠르게 번갈아 교차 (줌 스왑, 가속)
///  4. 화면 백색 섬광 → 새 모습 확정 (이 순간 실제 능력치가 적용된다)
///  5. 축하 메시지 후 게임 복귀
///
/// 컷씬 동안 Time.timeScale = 0 으로 게임을 정지하고, 시간은 언스케일드로 흐른다.
/// UI는 시작 화면과 같은 방식으로 런타임에 코드로 구성한다.
/// </summary>
public class EvolutionCutscene : MonoBehaviour
{
    public static EvolutionCutscene Instance { get; private set; }

    [Header("연출 리소스")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("포켓몬 스프라이트 확대 배율 (원본 픽셀 크기 기준)")]
    [SerializeField, Min(1f)] private float spriteScale = 8f;

    [Header("타이밍 (초)")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.4f;
    [SerializeField, Min(0f)] private float introHoldDuration = 1.2f;
    [SerializeField, Min(0f)] private float brightenDuration = 0.7f;
    [Tooltip("교차 연출 속도 배율. 클수록 빨리 끝난다 (1 = 원본 속도)")]
    [SerializeField, Min(0.1f)] private float swapSpeedMultiplier = 1.6f;
    [SerializeField, Min(0f)] private float flashInDuration = 0.25f;
    [SerializeField, Min(0f)] private float flashHoldDuration = 0.15f;
    [SerializeField, Min(0f)] private float flashOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float resultHoldDuration = 2.2f;

    [Header("메시지")]
    [SerializeField] private string introMessage = "어라...?! {0}이(가) 진화하려고 한다!";
    [SerializeField] private string resultMessage = "축하합니다! {0}은(는) {1}(으)로 진화했다!";
    [SerializeField] private string skipHint = "Space : 빨리 감기";

    public bool IsPlaying { get; private set; }

    // 원본 evoAnimation의 속도 수열: 느리게 시작해 점점 빨라진다.
    // 값은 "프레임(1/40초)당 줌 변화량" — 교차 1회 시간 = 1 / (vel * 40).
    private static readonly float[] swapVelocities = BuildSwapVelocities();

    // 실루엣 머티리얼은 재생마다 새로 만들면 누수되므로 한 번만 만들어 공유한다.
    private static Material silhouetteMaterial;

    private GameObject panelRoot;
    private CanvasGroup panelGroup;
    private RectTransform oldRoot, newRoot;
    private Image oldSilhouette, newSilhouette;
    private Image flashImage;
    private Text messageText;
    private bool skipRequested;

    // 에디터 프레임이 밀려도 연출이 통째로 건너뛰지 않도록 프레임당 시간 상한을 둔다.
    private static float Dt => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

    private static float[] BuildSwapVelocities()
    {
        // 원본 스크립트의 호출 횟수 그대로: 0.025×2, 0.05×3, 0.1×10, 0.2×4, 0.3×11, 0.4×21
        var list = new System.Collections.Generic.List<float>();
        void Add(float v, int n) { for (int i = 0; i < n; i++) list.Add(v); }
        Add(0.025f, 2); Add(0.05f, 3); Add(0.1f, 10); Add(0.2f, 4); Add(0.3f, 11); Add(0.4f, 21);
        return list.ToArray();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 컷씬을 재생한다. onReveal은 백색 섬광이 화면을 덮은 순간(새 모습 확정) 호출되며,
    /// 여기서 실제 진화(애니메이터·능력치 교체)를 수행하면 컷씬이 끝났을 때 이미 새 모습이다.
    /// </summary>
    public IEnumerator Play(Sprite oldSprite, Sprite newSprite, string oldName, string newName, Action onReveal)
    {
        if (IsPlaying) yield break;
        IsPlaying = true;
        skipRequested = false;

        float previousTimeScale = Time.timeScale;
        Canvas hudCanvas = UIManager.Instance != null ? UIManager.Instance.GetComponent<Canvas>() : null;
        bool hudWasEnabled = hudCanvas != null && hudCanvas.enabled;

        try
        {
            Time.timeScale = 0f;
            if (hudCanvas != null) hudCanvas.enabled = false;

            BuildUi(oldSprite, newSprite);
            SetZoom(oldRoot, 1f); SetZoom(newRoot, 0f);

            // 1. 페이드 인 + 안내 메시지
            yield return FadeCanvas(0f, 1f, fadeDuration);
            SetMessage(string.Format(introMessage, oldName));
            yield return Hold(introHoldDuration);

            // 2. 현재 모습이 흰 실루엣으로
            yield return FadeGraphic(oldSilhouette, 0f, 1f, brightenDuration);
            SetAlpha(newSilhouette, 1f); // 새 모습은 등장 순간부터 실루엣

            // 3. 줌 스왑 (점점 빨라짐, Space/클릭으로 빨리 감기)
            for (int i = 0; i < swapVelocities.Length && !skipRequested; i++)
            {
                bool toNew = i % 2 == 0; // 원본도 새 모습 먼저 보여주며 시작한다
                float duration = 1f / (swapVelocities[i] * 40f * swapSpeedMultiplier);
                yield return SwapStep(toNew, duration);
            }

            // 4. 백색 섬광 → 새 모습 확정
            SetMessage("");
            yield return FadeGraphic(flashImage, 0f, 1f, flashInDuration);
            onReveal?.Invoke();
            SetZoom(oldRoot, 0f); SetZoom(newRoot, 1f);
            SetAlpha(oldSilhouette, 0f); SetAlpha(newSilhouette, 0f);
            yield return Hold(flashHoldDuration);
            yield return FadeGraphic(flashImage, 1f, 0f, flashOutDuration);

            // 5. 축하 메시지
            SetMessage(string.Format(resultMessage, oldName, newName));
            yield return Hold(resultHoldDuration);
            yield return FadeCanvas(1f, 0f, fadeDuration);
        }
        finally
        {
            if (panelRoot != null) Destroy(panelRoot);
            if (hudCanvas != null) hudCanvas.enabled = hudWasEnabled;
            Time.timeScale = previousTimeScale;
            IsPlaying = false;
        }
    }

    // ---- 연출 단계 ----

    private IEnumerator SwapStep(bool toNew, float duration)
    {
        float from = toNew ? 1f : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            CheckSkip();
            if (skipRequested) yield break;
            elapsed += Dt;
            float t = Mathf.Clamp01(elapsed / duration);
            float oldZoom = toNew ? from - t : t;
            SetZoom(oldRoot, oldZoom);
            SetZoom(newRoot, 1f - oldZoom);
            yield return null;
        }
    }

    private IEnumerator Hold(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration) { elapsed += Dt; yield return null; }
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Dt;
            panelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        panelGroup.alpha = to;
    }

    private IEnumerator FadeGraphic(Graphic graphic, float from, float to, float duration)
    {
        if (graphic == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Dt;
            SetAlpha(graphic, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetAlpha(graphic, to);
    }

    private void CheckSkip()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if ((kb != null && kb.spaceKey.wasPressedThisFrame) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame))
            skipRequested = true;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    private void SetZoom(RectTransform root, float zoom)
    {
        if (root != null)
            root.localScale = Vector3.one * (spriteScale * Mathf.Clamp01(zoom));
    }

    private void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }

    // ---- UI 구성 (런타임 생성) ----

    private void BuildUi(Sprite oldSprite, Sprite newSprite)
    {
        panelRoot = new GameObject("EvolutionCutsceneUI");
        panelRoot.transform.SetParent(transform, false);

        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 32000;

        // 비트맵 폰트가 뭉개지지 않도록 캔버스 배율을 정수로 고정한다.
        CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = PixelUi.PixelScale;

        panelGroup = panelRoot.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        // 배경 (원본 evolutionbg를 화면에 꽉 채운다)
        Image bg = MakeStretched<Image>(panelRoot.transform, "Background");
        if (backgroundSprite != null) bg.sprite = backgroundSprite;
        else bg.color = new Color(0.05f, 0.35f, 0.3f);

        // 진화 전/후 스프라이트 (각각 원본 이미지 + 흰 실루엣)
        oldRoot = MakePokemon("OldForm", oldSprite, out oldSilhouette);
        newRoot = MakePokemon("NewForm", newSprite, out newSilhouette);

        // 하단 메시지 창
        Image msgBox = MakeStretched<Image>(panelRoot.transform, "MessageBox");
        msgBox.color = new Color(0f, 0f, 0f, 0.62f);
        msgBox.rectTransform.anchorMin = new Vector2(0f, 0f);
        msgBox.rectTransform.anchorMax = new Vector2(1f, 0f);
        msgBox.rectTransform.pivot = new Vector2(0.5f, 0f);
        msgBox.rectTransform.sizeDelta = new Vector2(0f, 150f);
        msgBox.rectTransform.anchoredPosition = Vector2.zero;

        Font font = PixelUi.Font;
        messageText = MakeStretched<Text>(msgBox.transform, "MessageText");
        messageText.font = font;
        messageText.fontSize = PixelUi.BaseFontSize * 3;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = Color.white;
        messageText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Text hint = MakeStretched<Text>(msgBox.transform, "SkipHint");
        hint.font = font;
        hint.fontSize = PixelUi.BaseFontSize * 2;
        hint.alignment = TextAnchor.LowerRight;
        hint.color = new Color(1f, 1f, 1f, 0.45f);
        hint.text = skipHint;
        hint.rectTransform.offsetMin = new Vector2(0f, 8f);
        hint.rectTransform.offsetMax = new Vector2(-16f, 0f);

        // 백색 섬광 (맨 위)
        flashImage = MakeStretched<Image>(panelRoot.transform, "Flash");
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.raycastTarget = false;
    }

    private RectTransform MakePokemon(string name, Sprite sprite, out Image silhouette)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(panelRoot.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 60f);

        // 원본 픽셀 크기 그대로 놓고, 확대는 루트 스케일(줌)로만 한다.
        GameObject imageGo = new GameObject("Sprite");
        imageGo.transform.SetParent(go.transform, false);
        Image image = imageGo.AddComponent<Image>();
        image.sprite = sprite;
        RectTransform imageRt = image.rectTransform;
        imageRt.anchorMin = imageRt.anchorMax = new Vector2(0.5f, 0.5f);
        imageRt.pivot = new Vector2(0.5f, 0.5f);
        imageRt.anchoredPosition = Vector2.zero;
        image.SetNativeSize();

        // 흰 실루엣: GUI/Text Shader는 텍스처의 알파만 사용해 단색 형태를 그린다.
        silhouette = MakeStretched<Image>(imageGo.transform, "Silhouette");
        silhouette.sprite = sprite;
        if (silhouetteMaterial == null)
        {
            Shader textShader = Shader.Find("GUI/Text Shader");
            if (textShader != null) silhouetteMaterial = new Material(textShader);
        }
        if (silhouetteMaterial != null) silhouette.material = silhouetteMaterial;
        silhouette.color = new Color(1f, 1f, 1f, 0f);

        return rt;
    }

    private static T MakeStretched<T>(Transform parent, string name) where T : Component
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        T comp = go.AddComponent<T>();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return comp;
    }
}
