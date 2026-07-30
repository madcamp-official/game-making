using UnityEngine;

/// <summary>
/// 자고 있는 NPC 머리 위에 뜨는 <b>Zzz 표시</b>.
///
/// 잠만보는 서 있는 그림 한 장으로 자는 중이라, 처음 보는 사람에게는 그냥 길을 막고 있는
/// 덩치로 보였다. "왜 말을 걸어야 하는가"가 그림에 없었다. 머리 위에 Zzz가 떠 있으면
/// 대사를 읽기 전에 자고 있다는 것이 먼저 읽힌다.
///
/// <b>크기는 상대가 정한다.</b> 붙일 대상의 스프라이트 폭에 견줘 잡으므로, 잠만보의 배율을
/// 바꿔도 표시가 따로 놀지 않는다. 고정 크기로 두었다면 몸집을 조정할 때마다 여기도 같이
/// 맞춰야 했을 것이다.
/// </summary>
public class SleepMark : MonoBehaviour
{
    /// <summary>한 번 떴다 가라앉는 데 걸리는 시간.</summary>
    private const float Period = 2.4f;

    /// <summary>떠오르는 높이. 대상 몸 높이에 대한 비율이다.</summary>
    private const float FloatFraction = 0.12f;

    /// <summary>사라질 때 잦아드는 시간.</summary>
    private const float FadeOut = 0.35f;

    private SpriteRenderer sprite;
    private Vector3 restPosition;
    private float floatHeight;
    private float phase;
    private float fading = -1f;

    /// <summary>
    /// 표시 하나를 세운다. <paramref name="target"/> 위에 얹히고 그 몸을 따라다닌다.
    /// </summary>
    /// <param name="heightFraction">
    /// 대상 몸 높이의 몇 할로 그릴지.
    ///
    /// <b>폭이 아니라 높이를 기준으로 잡는다.</b> Zzz 그림은 세로로 긴 기둥꼴(32×48)이라
    /// 폭으로 맞추면 높이가 1.5배로 늘어난다 — 폭을 몸의 네 할로 두었더니 표시가 잠만보보다
    /// 키가 커져서, 자는 표시가 아니라 머리 위에 세운 간판처럼 보였다.
    /// </param>
    /// <param name="gapFraction">몸 위쪽 끝에서 얼마나 띄울지 (몸 높이에 대한 비율).</param>
    public static SleepMark Create(SpriteRenderer target, Sprite mark,
                                   float heightFraction = 0.7f, float gapFraction = 0.06f)
    {
        if (target == null || mark == null) return null;

        var go = new GameObject("SleepMark");
        go.transform.SetParent(target.transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = mark;
        // 몸보다 앞에 그린다. 같은 층에 두면 머리와 겹치는 자리에서 가려진다.
        sr.sortingLayerID = target.sortingLayerID;
        sr.sortingOrder = target.sortingOrder + 1;

        var self = go.AddComponent<SleepMark>();
        self.sprite = sr;
        self.Fit(target, mark, heightFraction, gapFraction);
        return self;
    }

    /// <summary>
    /// 대상 몸에 맞춰 크기와 자리를 잡는다.
    ///
    /// ⚠️ 부모(잠만보)가 이미 배율을 갖고 있어서(2.2배) localScale을 그대로 주면 그만큼 더
    /// 커진다. 원하는 <b>월드</b> 크기를 부모 배율로 나눠 되돌려야 한다. 자리도 마찬가지라
    /// 부모의 로컬 좌표로 환산해서 올린다.
    /// </summary>
    private void Fit(SpriteRenderer target, Sprite mark, float heightFraction, float gapFraction)
    {
        Bounds body = target.bounds;                  // 월드 기준 몸 크기
        Vector3 parentScale = target.transform.lossyScale;

        float markWorldHeightAtOne = mark.bounds.size.y;   // 배율 1일 때의 월드 높이
        if (markWorldHeightAtOne <= 0.0001f) markWorldHeightAtOne = 1f;

        float wanted = body.size.y * Mathf.Max(0.01f, heightFraction);
        float worldScale = wanted / markWorldHeightAtOne;

        float sx = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
        float sy = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;
        transform.localScale = new Vector3(worldScale / sx, worldScale / sy, 1f);

        // 몸 위쪽 끝에서 조금 띄우고, 표시 자신의 절반만큼 더 올려 아래끝이 몸에 닿게 한다.
        float markWorldHeight = mark.bounds.size.y * worldScale;
        float topWorld = body.max.y + body.size.y * gapFraction + markWorldHeight * 0.5f;
        float upFromCenter = (topWorld - target.transform.position.y) / sy;
        restPosition = new Vector3(0f, upFromCenter, 0f);
        transform.localPosition = restPosition;

        floatHeight = body.size.y * FloatFraction / sy;
    }

    /// <summary>잠에서 깼다. 잦아들며 사라진다.</summary>
    public void Dismiss()
    {
        if (fading < 0f) fading = 0f;
    }

    private void Update()
    {
        // 대사창이 시간을 세워 두므로(timeScale 0) 실제 시간으로 센다 — 안 그러면 이벤트를
        // 여는 순간 Zzz가 굳어 버린다.
        float dt = Time.unscaledDeltaTime;

        if (fading >= 0f)
        {
            fading += dt;
            float t = Mathf.Clamp01(fading / FadeOut);
            SetAlpha(1f - t);
            if (t >= 1f) Destroy(gameObject);
            return;
        }

        phase += dt * (Mathf.PI * 2f / Period);
        float wave = Mathf.Sin(phase);
        transform.localPosition = restPosition + new Vector3(0f, wave * floatHeight * 0.5f, 0f);
        // 떠오를 때 진해지고 가라앉을 때 옅어진다. 숨을 쉬는 것처럼 보이게 하려는 것이다.
        SetAlpha(0.65f + 0.35f * (wave * 0.5f + 0.5f));
    }

    private void SetAlpha(float alpha)
    {
        if (sprite == null) return;
        Color c = sprite.color;
        c.a = Mathf.Clamp01(alpha);
        sprite.color = c;
    }
}
