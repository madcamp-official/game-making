using UnityEngine;

/// <summary>
/// 신뇽이 까는 해류. <paramref name="area"/> 전체를 한 방향으로 흐르게 만들어,
/// 그 안의 플레이어를 표시된 방향으로 계속 민다.
///
/// 피해는 없고 플레이어에게만 적용된다 — 적·투사체·예고는 영향을 받지 않는다.
/// 갸라도스의 삼중 해류를 미리 맛보게 하는 장치라 겉모습(바닥 색 + 흐르는 화살표)은
/// 그쪽을 닮게 하되, 방향이 언제나 하나뿐이라는 점이 다르다.
///
/// 화살표는 한 줄이 아니라 격자로 편다. 맵 전체가 흐르는데 가운데 한 줄만 그리면
/// 그 줄만 위험한 것처럼 읽히기 때문이다.
///
/// 시간이 다 되거나 주인(신뇽)이 죽으면 스스로 사라진다. 방이 끝난 뒤 해류가
/// 남는 일이 없도록, 수명 관리는 전부 이 컴포넌트 안에서 끝낸다 — 바닥 색과
/// 화살표는 언제나 같이 나고 같이 진다.
/// </summary>
public class CurrentBand : MonoBehaviour
{
    /// <summary>흐름과 직각으로 늘어놓는 줄 수.</summary>
    private const int Lines = 3;
    /// <summary>한 줄에 놓는 화살표 수.</summary>
    private const int PerLine = 6;

    private Rect area;
    private Vector2 pushDirection;
    private float pushSpeed;
    private float expireAt;
    private float fadeDuration = 0.35f;

    private Health owner;
    private PlayerCrowdControl playerCc;
    private Transform player;

    private SpriteRenderer bandRenderer;
    private SpriteRenderer[] arrows;
    /// <summary>흐름이 가로인지. 화살표 배치와 미는 축을 여기서 갈라 쓴다.</summary>
    private bool horizontal;
    private float scroll;
    private Color bandColor;
    private Color arrowColor;
    private bool fading;
    private float fadeStart;

    /// <summary>
    /// 띠를 깐다. <paramref name="area"/>는 실제로 미는 범위와 같은 크기로 그려진다.
    /// </summary>
    public static CurrentBand Spawn(Transform parent, Rect area, Vector2 pushDirection,
                                    float pushSpeed, float duration, Health owner,
                                    Color bandColor, Color arrowColor)
    {
        GameObject go = EnemyEffect.Mark(new GameObject("CurrentBand"));
        go.transform.SetParent(parent, false);
        go.transform.position = area.center;

        CurrentBand band = go.AddComponent<CurrentBand>();
        band.area = area;
        band.pushDirection = pushDirection.normalized;
        band.pushSpeed = pushSpeed;
        band.expireAt = Time.time + duration;
        band.owner = owner;
        band.bandColor = bandColor;
        band.arrowColor = arrowColor;
        band.BuildVisuals();
        return band;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerCc = PlayerCrowdControl.Of(pc);
        }
    }

    private void BuildVisuals()
    {
        bandRenderer = gameObject.AddComponent<SpriteRenderer>();
        bandRenderer.sprite = PrimitiveSprites.Square;
        bandRenderer.color = bandColor;
        bandRenderer.sortingOrder = -1; // 지형 위, 예고(0)와 캐릭터 아래
        transform.localScale = new Vector3(area.width, area.height, 1f);

        horizontal = Mathf.Abs(pushDirection.x) > Mathf.Abs(pushDirection.y);

        // 흐르는 축으로는 PerLine칸, 직각 축으로는 Lines칸으로 나눈다. 둘 중 좁은 칸에
        // 맞춰야 맵이 가로로 길어도 화살표가 서로 겹치지 않는다.
        float alongSpan = horizontal ? area.width : area.height;
        float acrossSpan = horizontal ? area.height : area.width;
        float arrowSize = Mathf.Min(alongSpan / PerLine, acrossSpan / Lines) * 0.55f;

        arrows = new SpriteRenderer[Lines * PerLine];
        for (int i = 0; i < arrows.Length; i++)
        {
            // 부모 배율(해류 크기)에 눌리지 않도록 띠의 자식이 아니라 형제로 두고 위치만 따라간다.
            //
            // ⚠️ 그래서 표식이 반드시 필요하다. 방을 정리할 때(EnemyEffect.ClearUnder)
            // 표식이 붙은 띠만 지워지고 형제인 화살표는 살아남아, 신뇽을 잡은 뒤 빈 방
            // 바닥에 화살표만 떠 있었다. 띠와 같은 표식을 달아 함께 걷히게 한다.
            GameObject go = EnemyEffect.Mark(new GameObject("Arrow"));
            go.transform.SetParent(transform.parent, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprites.Triangle;
            sr.color = arrowColor;
            sr.sortingOrder = 0;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.right, pushDirection);
            go.transform.localScale = new Vector3(arrowSize * 1.15f, arrowSize * 0.9f, 1f);
            arrows[i] = sr;
        }
    }

    private void Update()
    {
        bool ownerDead = owner == null || owner.IsDead;
        if (!fading && (Time.time >= expireAt || ownerDead))
        {
            fading = true;
            fadeStart = Time.time;
        }

        float alpha = 1f;
        if (fading)
        {
            alpha = 1f - Mathf.Clamp01((Time.time - fadeStart) / fadeDuration);
            if (alpha <= 0f) { Cleanup(); return; }
        }

        // 화살표를 미는 방향으로 흘려보낸다. 한 바퀴 돌면 제자리다.
        float span = horizontal ? area.width : area.height;
        float acrossSpan = horizontal ? area.height : area.width;
        float spacing = span / PerLine;
        float lineSpacing = acrossSpan / Lines;
        float alongMin = horizontal ? area.xMin : area.yMin;
        float acrossMin = horizontal ? area.yMin : area.xMin;
        bool forward = horizontal ? pushDirection.x > 0f : pushDirection.y > 0f;
        scroll += Time.deltaTime * 1.6f;

        Color c = arrowColor;
        c.a = arrowColor.a * alpha;

        for (int line = 0; line < Lines; line++)
        {
            // 줄은 직각 축에 고르게 편다. 반 칸 띄워야 맨 위·아래 줄이 벽에 걸치지 않는다.
            float across = acrossMin + lineSpacing * (line + 0.5f);
            // 줄마다 반 칸씩 어긋내면 화살표가 세로로 줄 맞춰 서지 않아 흐름처럼 보인다.
            float stagger = (line % 2) * spacing * 0.5f;

            for (int i = 0; i < PerLine; i++)
            {
                SpriteRenderer sr = arrows[line * PerLine + i];
                if (sr == null) continue;

                float along = Mathf.Repeat(i * spacing + spacing * 0.5f + stagger + scroll, span);
                float at = alongMin + (forward ? along : span - along);
                sr.transform.position = horizontal ? new Vector2(at, across) : new Vector2(across, at);
                sr.color = c;
            }
        }
        Color bc = bandColor; bc.a = bandColor.a * alpha;
        if (bandRenderer != null) bandRenderer.color = bc;
    }

    private void FixedUpdate()
    {
        if (fading || playerCc == null || player == null) return;
        if (area.Contains(player.position))
            playerCc.AddVelocity(pushDirection * pushSpeed);
    }

    private void Cleanup()
    {
        // 화살표는 OnDestroy가 치운다 — 띠가 어떤 길로 사라지든 같은 자리를 지나게 한다.
        Destroy(gameObject);
    }

    /// <summary>
    /// 띠가 사라지는 모든 길에서 화살표를 함께 걷는다.
    ///
    /// 수명이 다한 길(<see cref="Cleanup"/>)만 막아서는 모자랐다. 방 정리가 띠를 바로
    /// 지워 버리는 길이 따로 있어서, 그때는 화살표를 치울 사람이 아무도 없었다.
    /// </summary>
    private void OnDestroy()
    {
        if (arrows == null) return;
        foreach (SpriteRenderer arrow in arrows)
            if (arrow != null) Destroy(arrow.gameObject);
        arrows = null;
    }
}
