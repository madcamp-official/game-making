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
    /// <summary>
    /// 포켓몬 스프라이트 확대 배율 (원본 픽셀 크기 기준). 80×80 그림이 5배면 화면 400×400이다.
    ///
    /// ⚠️ <b>정수로 둘 것.</b> 그림은 점 필터로 그리는 픽셀 아트라, 4.5배 같은 값을 주면 원본
    /// 한 픽셀이 화면 네 픽셀과 다섯 픽셀에 번갈아 걸쳐 획 굵기가 자리마다 달라진다.
    /// 그래서 크기를 조금 키우려 해도 한 단계가 곧 25%다.
    /// </summary>
    [Tooltip("포켓몬 스프라이트 확대 배율 (원본 픽셀 크기 기준). 점 필터라 정수로 둘 것.")]
    [SerializeField, Min(1f)] private float spriteScale = 5f;

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
    [SerializeField] private string cancelMessage = "어라...? {0}은(는) 진화를 그만두었다!";
    [SerializeField] private string skipHint = "Space : 빨리 감기      B : 진화 취소";

    [Header("취소 연출")]
    [Tooltip("취소했을 때 원래 모습으로 되돌아오는 데 걸리는 시간.")]
    [SerializeField, Min(0f)] private float cancelSettleDuration = 0.5f;
    [Tooltip("취소 문구를 보여 주는 시간.")]
    [SerializeField, Min(0f)] private float cancelHoldDuration = 2f;

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
    private bool cancelRequested;

    /// <summary>
    /// 지난 재생이 취소로 끝났는가. 부르는 쪽(<see cref="PlayerEvolution"/>)이 단계를 되돌릴지
    /// 판단하는 데 쓴다 — 컷씬은 연출만 맡고, 실제로 무엇을 되돌릴지는 그쪽이 안다.
    /// </summary>
    public bool WasCancelled { get; private set; }

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
    /// 빨리 감기·취소 입력은 여기서 본다.
    ///
    /// 연출 단계마다 따로 보지 않는 이유: 예전에는 교차 연출 안에서만 입력을 봤는데, 그래서
    /// 안내 문구가 떠 있는 1.2초와 몸이 빛나는 0.7초 동안 누른 키가 통째로 무시됐다. 진화가
    /// 시작되자마자 B를 누르는 것이 가장 자연스러운 자리인데 하필 그 자리가 먹통이었다.
    ///
    /// <see cref="Update"/>는 <see cref="Time.timeScale"/>이 0이어도 매 프레임 돈다 — 컷씬이
    /// 시간을 세워 두었어도 입력은 그대로 들어온다.
    /// </summary>
    private void Update()
    {
        if (IsPlaying && acceptingInput) CheckSkip();
    }

    /// <summary>지금 입력을 받는 구간인가. 결과가 확정된 뒤에는 되돌릴 것이 없으므로 닫는다.</summary>
    private bool acceptingInput;

    /// <summary>
    /// 컷씬을 재생한다. onReveal은 백색 섬광이 화면을 덮은 순간(새 모습 확정) 호출되며,
    /// 여기서 실제 진화(애니메이터·능력치 교체)를 수행하면 컷씬이 끝났을 때 이미 새 모습이다.
    /// </summary>
    public IEnumerator Play(Sprite oldSprite, Sprite newSprite, string oldName, string newName, Action onReveal)
    {
        if (IsPlaying) yield break;
        IsPlaying = true;
        skipRequested = false;
        cancelRequested = false;
        WasCancelled = false;

        float previousTimeScale = Time.timeScale;
        CanvasGroup hud = HudGroup();
        float hudAlpha = hud != null ? hud.alpha : 1f;

        try
        {
            Time.timeScale = 0f;
            if (hud != null) hud.alpha = 0f;

            BuildUi(oldSprite, newSprite);
            SetZoom(oldRoot, 1f); SetZoom(newRoot, 0f);

            // 1. 페이드 인 + 안내 메시지. 여기서부터 진화음이 깔린다 —
            //    몸이 빛나기 시작하는 것과 소리가 시작되는 것이 같은 순간이어야 한다.
            yield return FadeCanvas(0f, 1f, fadeDuration);
            GameAudio.PlayEvolving();
            acceptingInput = true;
            SetMessage(string.Format(introMessage, oldName));
            yield return HoldOrInterrupt(introHoldDuration);

            // 2. 현재 모습이 흰 실루엣으로
            yield return FadeGraphicOrInterrupt(oldSilhouette, 0f, 1f, brightenDuration);
            SetAlpha(newSilhouette, 1f); // 새 모습은 등장 순간부터 실루엣

            // 3. 줌 스왑 (점점 빨라짐, Space/클릭으로 빨리 감기, B로 취소)
            for (int i = 0; i < swapVelocities.Length && !skipRequested && !cancelRequested; i++)
            {
                bool toNew = i % 2 == 0; // 원본도 새 모습 먼저 보여주며 시작한다
                float duration = 1f / (swapVelocities[i] * 40f * swapSpeedMultiplier);
                yield return SwapStep(toNew, duration);
            }

            if (cancelRequested)
            {
                // 4-A. 취소 — 새 모습을 지우고 원래 모습으로 되돌아간다. onReveal을 부르지
                //      않으므로 능력치도 애니메이터도 손대지 않은 채로 남는다.
                yield return CancelRoutine(oldName);
            }
            else
            {
                // 4-B. 백색 섬광 → 새 모습 확정. 진화음이 여기서 끊기고 완료음으로 갈아탄다.
                //      여기서부터는 되돌릴 것이 없으므로 입력을 닫는다.
                acceptingInput = false;
                SetMessage("");
                yield return FadeGraphic(flashImage, 0f, 1f, flashInDuration);
                GameAudio.PlayEvolved();
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
        }
        finally
        {
            // 연출이 어떻게 끝나든(확정·취소·도중에 끊김) 진화음은 반드시 멈춘다.
            // 이 소리는 전용 재생기에서 도므로, 놓치면 컷씬이 사라진 뒤에도 계속 울린다.
            GameAudio.StopEvolving();
            if (panelRoot != null) Destroy(panelRoot);
            if (hud != null) hud.alpha = hudAlpha;
            Time.timeScale = previousTimeScale;
            IsPlaying = false;
        }
    }

    /// <summary>
    /// 진화를 그만둔다. 새 모습을 지우고 원래 모습을 제자리로 되돌린 뒤, 그만두었다고 알린다.
    ///
    /// 섬광을 터뜨리지 않는 것이 핵심이다 — 섬광은 "확정됐다"는 신호라, 취소에도 번쩍이면
    /// 무엇이 일어난 것인지 읽을 수가 없다. 대신 실루엣이 사그라들며 원래 모습이 돌아온다.
    /// </summary>
    private IEnumerator CancelRoutine(string oldName)
    {
        WasCancelled = true;
        acceptingInput = false;
        GameAudio.StopEvolving();

        SetMessage("");

        // 새 모습을 접고 원래 모습을 온전히 되돌린다. 교차 도중 어느 쪽이 얼마나 보이던
        // 중이었든 여기서 하나로 수렴한다.
        float from = oldRoot != null ? oldRoot.localScale.x / Mathf.Max(0.0001f, spriteScale) : 0f;
        float elapsed = 0f;
        while (elapsed < cancelSettleDuration)
        {
            elapsed += Dt;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, cancelSettleDuration));
            SetZoom(oldRoot, Mathf.Lerp(from, 1f, t));
            SetZoom(newRoot, Mathf.Lerp(1f - from, 0f, t));
            // 흰 실루엣이 벗겨지며 원래 색이 드러난다.
            SetAlpha(oldSilhouette, 1f - t);
            SetAlpha(newSilhouette, 1f - t);
            yield return null;
        }
        SetZoom(oldRoot, 1f); SetZoom(newRoot, 0f);
        SetAlpha(oldSilhouette, 0f); SetAlpha(newSilhouette, 0f);

        SetMessage(string.Format(cancelMessage, oldName));
        yield return Hold(cancelHoldDuration);
        yield return FadeCanvas(1f, 0f, fadeDuration);
    }

    /// <summary>
    /// 컷씬 동안 HUD를 가리는 손잡이. <b>Canvas를 끄지 않고</b> 투명도만 0으로 내린다.
    ///
    /// 예전에는 <c>Canvas.enabled = false</c>로 껐는데, 그동안 글자가 다시 그려지면
    /// 아주 작게 굳어 버렸다. uGUI의 <c>Text</c>는 비트맵 폰트를 그릴 때
    /// <c>폰트 기준 크기(12) / 요청 크기(24)</c>로 확대율을 잡는데, 그 계산이 자기 위에 있는
    /// <b>켜져 있는</b> Canvas를 못 찾으면 확대율을 1로 떨어뜨린다. 그러면 24픽셀로 그릴
    /// 글자가 12픽셀로 그려진다.
    ///
    /// 하필 이 컷씬 한가운데(백색 섬광)에서 최대 체력이 오르고 기술을 하나 배운다. 그 두 값이
    /// 바로 왼쪽 아래 체력 HUD와 기술 칸 HUD의 글자다 — 마침 그때 다시 그려지고, 다시 그릴
    /// 일이 없어 작아진 채로 남았다. Canvas를 켜 둔 채로 숨기면 확대율이 흔들리지 않는다.
    /// </summary>
    private static CanvasGroup HudGroup()
    {
        if (UIManager.Instance == null) return null;
        CanvasGroup group = UIManager.Instance.GetComponent<CanvasGroup>();
        return group != null ? group : UIManager.Instance.gameObject.AddComponent<CanvasGroup>();
    }

    // ---- 연출 단계 ----

    private IEnumerator SwapStep(bool toNew, float duration)
    {
        float from = toNew ? 1f : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 빨리 감기든 취소든 이 교차는 즉시 접는다. 빨리 감기는 곧장 섬광으로,
            // 취소는 곧장 되돌리는 연출로 넘어간다. 입력은 Update가 본다.
            if (skipRequested || cancelRequested) yield break;
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

    /// <summary>지금 빨리 감기나 취소가 걸려 있는가.</summary>
    private bool Interrupted => skipRequested || cancelRequested;

    /// <summary>
    /// 기다리다가 입력이 들어오면 즉시 끊는다. 섬광 앞의 구간에만 쓴다.
    ///
    /// 이 구간을 끊지 않으면 눌러도 최대 1.2초를 더 기다려야 해서, 키가 씹힌 것처럼 느껴진다.
    /// </summary>
    private IEnumerator HoldOrInterrupt(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !Interrupted) { elapsed += Dt; yield return null; }
    }

    /// <summary>같은 이유로 중간에 끊을 수 있는 페이드. 끊기면 목표값으로 바로 맞춰 둔다.</summary>
    private IEnumerator FadeGraphicOrInterrupt(Graphic graphic, float from, float to, float duration)
    {
        if (graphic == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration && !Interrupted)
        {
            elapsed += Dt;
            SetAlpha(graphic, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetAlpha(graphic, to);
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

    /// <summary>
    /// 빨리 감기와 취소 입력을 본다.
    ///
    /// 취소가 빨리 감기를 이긴다. 한 프레임에 둘 다 눌렸다면 그만두려는 쪽이 분명한 뜻이다 —
    /// 빨리 감기는 "결과를 빨리 보고 싶다"이지만 취소는 "그 결과를 원하지 않는다"이다.
    /// </summary>
    private void CheckSkip()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null && kb.bKey.wasPressedThisFrame)
        {
            cancelRequested = true;
            return;
        }

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

        // 오버레이로 그린다. 예전에는 ScreenSpaceCamera + planeDistance 1이었는데, 그 방식은
        // 캔버스를 카메라 앞 월드 공간에 놓고 화면 크기로 되돌리는 과정을 거쳐서, 화면 크기가
        // 딱 떨어지지 않으면 가장자리에 실낱 같은 틈이 생기고 그리로 뒤 화면이 비쳤다.
        // 전체 화면을 덮는 것이 전부인 연출이라 카메라를 거칠 이유가 없다.
        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        // 비트맵 폰트가 뭉개지지 않도록 캔버스 배율을 정수로 고정한다.
        CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = PixelUi.PixelScale;

        panelGroup = panelRoot.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        // 배경 (원본 evolutionbg를 화면에 꽉 채운다)
        Image bg = MakeStretched<Image>(panelRoot.transform, "Background");
        Bleed(bg.rectTransform);
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
        Bleed(flashImage.rectTransform);
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.raycastTarget = false;
    }

    /// <summary>화면을 덮는 것이 목적인 판을 가장자리 밖까지 조금 더 키운다.</summary>
    private const float BleedPixels = 4f;

    private static void Bleed(RectTransform rt)
    {
        rt.offsetMin = new Vector2(-BleedPixels, -BleedPixels);
        rt.offsetMax = new Vector2(BleedPixels, BleedPixels);
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
