using UnityEngine;

/// <summary>
/// 격류 압착에 쓰는 사각 범람 영역.
///
/// <see cref="DamageZone"/>은 원형 고정 장판 전용이라 여기에 억지로 끼워 넣지 않았다.
/// 범람은 전투장 한쪽 변 전체를 덮는 직사각형이고, 예고 → 활성 → 후퇴의 세 단계를 거치며,
/// 맞은 플레이어를 잠시 느리게 만든다. 그려진 사각형이 곧 피해 범위다.
///
/// 물 자체는 플레이어를 밀지 않는다. 강제 이동은 삼중 해류 하나로 통일한다.
/// </summary>
public class GyaradosFloodZone : MonoBehaviour
{
    /// <summary>보스 장판과 같은 높이. 지형보다 위, 캐릭터보다 아래.</summary>
    private const int FloodSortingOrder = 1;
    /// <summary>물이 차오른 가장자리를 나누는 흰 거품선의 두께.</summary>
    private const float FoamThickness = 0.16f;

    private Rect area;
    private SpriteRenderer body;

    private bool active;
    private int damage;
    private float retryInterval;
    private float slowMultiplier = 1f;
    private float slowDuration;
    private float nextDamageTime;
    private WaterCurrentField currentField;

    private Transform player;
    private Health playerHealth;

    private Color floodColor;

    /// <summary>
    /// 예고 상태로 만든다. 아직 피해는 없지만 활성화됐을 때와 <b>같은 크기</b>로 그린다.
    /// </summary>
    /// <param name="inwardEdge">물이 차오르는 안쪽 면의 방향. 거품선을 그 변에 붙인다.</param>
    public static GyaradosFloodZone Spawn(Transform parent, Rect area, Vector2 inwardEdge,
                                          Color warningColor, Color floodColor, Color foamColor)
    {
        GameObject go = new GameObject("FloodZone");
        go.transform.SetParent(parent, false);
        go.transform.position = area.center;

        SpriteRenderer body = go.AddComponent<SpriteRenderer>();
        body.sprite = PrimitiveSprites.Square;
        body.color = warningColor;
        body.sortingOrder = FloodSortingOrder;
        go.transform.localScale = new Vector3(area.width, area.height, 1f);

        // 거품선은 자식으로 두되 부모 배율에 눌리지 않게 월드 크기로 다시 잡는다.
        GameObject foamGo = new GameObject("Foam");
        foamGo.transform.SetParent(go.transform, false);
        SpriteRenderer foam = foamGo.AddComponent<SpriteRenderer>();
        foam.sprite = PrimitiveSprites.Square;
        foam.color = foamColor;
        foam.sortingOrder = FloodSortingOrder + 1;

        bool horizontal = Mathf.Abs(inwardEdge.x) > Mathf.Abs(inwardEdge.y);
        Vector2 edgeCenter = new Vector2(
            area.center.x + (horizontal ? inwardEdge.x * area.width * 0.5f : 0f),
            area.center.y + (horizontal ? 0f : inwardEdge.y * area.height * 0.5f));
        foamGo.transform.position = edgeCenter;
        foamGo.transform.localScale = new Vector3(
            horizontal ? FoamThickness / Mathf.Max(0.01f, area.width) : 1f,
            horizontal ? 1f : FoamThickness / Mathf.Max(0.01f, area.height), 1f);

        GyaradosFloodZone zone = go.AddComponent<GyaradosFloodZone>();
        zone.area = area;
        zone.body = body;
        zone.floodColor = floodColor;
        return zone;
    }

    /// <summary>물이 실제로 차올라 피해 영역이 된다.</summary>
    public void Activate(int damageAmount, float interval, float slowFactor, float slowSeconds,
                         WaterCurrentField field)
    {
        active = true;
        damage = damageAmount;
        retryInterval = Mathf.Max(0.05f, interval);
        slowMultiplier = slowFactor;
        slowDuration = slowSeconds;
        currentField = field;
        // 들어서는 순간 바로 한 번 시도한다.
        nextDamageTime = 0f;
        if (body != null) body.color = floodColor;
    }

    /// <summary>물이 빠진다. 남아 있던 감속은 새로 걸지 않고 그대로 만료된다.</summary>
    public void Recede()
    {
        active = false;
        Destroy(gameObject);
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        player = pc.transform;
        playerHealth = pc.GetComponent<Health>();
    }

    private void Update()
    {
        if (!active || damage <= 0) return;
        if (player == null || playerHealth == null || playerHealth.IsDead) return;

        if (!area.Contains(player.position))
        {
            // 밖으로 나갔다가 다시 들어오면 곧바로 다시 맞는다.
            nextDamageTime = 0f;
            return;
        }

        if (Time.time < nextDamageTime) return;
        // 공통 무적 중이면 이번 프레임만 건너뛴다. 재시도 시각은 미루지 않으므로
        // 무적이 끝나는 순간 안에 서 있으면 바로 다시 맞는다.
        if (playerHealth.IsInvincible) return;

        playerHealth.TakeDamage(damage);
        nextDamageTime = Time.time + retryInterval;
        if (currentField != null) currentField.ApplyPlayerSlow(slowMultiplier, slowDuration);
    }
}
