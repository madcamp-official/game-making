using UnityEngine;

/// <summary>
/// 신뇽이 까는 단일 해류 띠. 전투장을 가로지르는 가로 또는 세로 띠 하나로,
/// 안에 있는 플레이어를 표시된 방향으로 계속 민다.
///
/// 피해는 없고 플레이어에게만 적용된다 — 적·투사체·예고는 영향을 받지 않는다.
/// 갸라도스의 삼중 해류를 미리 맛보게 하는 장치라 겉모습(띠 + 흐르는 화살표)은
/// 그쪽을 닮게 하되, 동시에 한 줄뿐이라는 점이 다르다.
///
/// 시간이 다 되거나 주인(신뇽)이 죽으면 스스로 사라진다. 방이 끝난 뒤 해류가
/// 남는 일이 없도록, 수명 관리는 전부 이 컴포넌트 안에서 끝낸다.
/// </summary>
public class CurrentBand : MonoBehaviour
{
    private const int ArrowsCount = 6;

    private Rect area;
    private Vector2 pushDirection;
    private float pushSpeed;
    private float expireAt;
    private float fadeDuration = 0.35f;

    private Health owner;
    private PlayerCrowdControl playerCc;
    private Transform player;

    private SpriteRenderer bandRenderer;
    private readonly SpriteRenderer[] arrows = new SpriteRenderer[ArrowsCount];
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
        GameObject go = new GameObject("CurrentBand");
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

        float arrowSize = Mathf.Min(area.width, area.height) * 0.5f;
        for (int i = 0; i < ArrowsCount; i++)
        {
            GameObject go = new GameObject("Arrow");
            // 부모 배율(띠 크기)에 눌리지 않도록 월드에 두고 위치만 따라간다.
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

        // 화살표를 미는 방향으로 흘려보낸다. 띠 길이를 한 바퀴 돌면 제자리다.
        bool horizontal = Mathf.Abs(pushDirection.x) > Mathf.Abs(pushDirection.y);
        float span = horizontal ? area.width : area.height;
        float spacing = span / ArrowsCount;
        scroll += Time.deltaTime * 1.6f;

        for (int i = 0; i < ArrowsCount; i++)
        {
            if (arrows[i] == null) continue;
            float along = Mathf.Repeat(i * spacing + spacing * 0.5f + scroll, span);
            Vector2 at = horizontal
                ? new Vector2(area.xMin + (pushDirection.x > 0f ? along : span - along), area.center.y)
                : new Vector2(area.center.x, area.yMin + (pushDirection.y > 0f ? along : span - along));
            arrows[i].transform.position = at;
            Color c = arrowColor; c.a = arrowColor.a * alpha;
            arrows[i].color = c;
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
        foreach (SpriteRenderer arrow in arrows)
            if (arrow != null) Destroy(arrow.gameObject);
        Destroy(gameObject);
    }
}
