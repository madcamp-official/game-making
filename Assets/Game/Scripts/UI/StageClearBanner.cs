using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방을 정리했을 때 화면 한가운데에 잠깐 뜨는 큰 글씨. 잠시 머물다 서서히 사라진다.
///
/// 안내문 한 줄(<see cref="UIManager.ShowMessage"/>)로 때우지 않는 이유: 이 순간은 방 하나가
/// 끝났다는 <b>마디</b>다. 마지막 적이 쓰러지자마자 강화 팔레트가 튀어나오면 방금 무슨 일이
/// 있었는지 볼 새가 없다. 글씨가 사라지는 시간이 곧 숨 돌리는 시간이다.
///
/// 사라지는 동안에도 게임은 멈추지 않는다. 시간을 세우는 것은 그 뒤에 뜨는 강화 팔레트의
/// 몫이고, 여기서까지 멈추면 "잡았다"는 손맛이 끊긴다.
/// </summary>
public class StageClearBanner : MonoBehaviour
{
    /// <summary>글씨 크기. PMDFont는 12의 배수만 또렷하게 나온다.</summary>
    private const int FontSize = 48;

    private Text label;
    private Coroutine running;

    public static StageClearBanner Create(Transform canvasRoot)
    {
        GameObject go = new GameObject("StageClearBanner", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(canvasRoot, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var banner = go.AddComponent<StageClearBanner>();
        banner.label = PixelUi.MakeText(rt, "Label", FontSize, Color.white, TextAnchor.MiddleCenter);
        RectTransform lrt = banner.label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        // 화면 한가운데보다 조금 위. 정중앙은 주인공과 겹쳐 글씨가 몸에 걸린다.
        //
        // ⚠️ 올리려면 <b>아래 변을 끌어올려야</b> 한다(offsetMin). 예전에는 위 변을 내렸는데
        // (offsetMax −120) 그러면 상자 가운데가 오히려 아래로 내려가, 글씨가 정확히 피하려던
        // 자리 — 주인공의 몸 — 에 앉았다. "스테이지 시작!"을 붙이면서 눈에 띄었다.
        lrt.offsetMin = new Vector2(0f, 120f);
        lrt.offsetMax = Vector2.zero;
        banner.SetAlpha(0f);
        return banner;
    }

    /// <summary>글씨를 띄우고 <paramref name="hold"/>만큼 두었다가 <paramref name="fade"/> 동안 지운다.</summary>
    public Coroutine Show(string text, float hold, float fade)
    {
        if (label == null) return null;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Routine(text, hold, fade));
        return running;
    }

    private IEnumerator Routine(string text, float hold, float fade)
    {
        label.text = text;
        SetAlpha(1f);

        // 실제 시간으로 센다. 이 뒤에 뜨는 강화 팔레트가 시간을 세우는데, 스케일 시간으로
        // 재면 팔레트가 열려 있는 내내 글씨가 화면에 눌어붙는다.
        yield return new WaitForSecondsRealtime(hold);

        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / fade));
            yield return null;
        }

        SetAlpha(0f);
        label.text = "";
        running = null;
    }

    private void SetAlpha(float a)
    {
        if (label == null) return;
        Color c = label.color;
        c.a = a;
        label.color = c;
    }
}
