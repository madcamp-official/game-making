using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 갸라도스의 해류 굴절 하이드로펌프. 위 또는 아래 외곽 바다에서 들어와 수로 경계를 지날 때마다
/// 그 수로의 흐름에 끌려 좌우로 꺾인다.
///
/// 경로는 발사 전에 <see cref="BuildPath"/>로 한 번에 계산해 구간 목록으로 들고 있는다.
/// 물리 레이캐스트를 쓰지 않으므로 장식 콜라이더나 잉어킹 때문에 경로가 달라지지 않는다 —
/// 플레이어가 화살표만 보고 굴절을 예측할 수 있어야 하기 때문이다.
///
/// 선두가 지나간 구간은 정해진 시간 동안 피해 판정으로 남는다. 굵기와 판정은 굴절 전후가 같다.
/// </summary>
public class GyaradosHydroBeam : MonoBehaviour
{
    /// <summary>물줄기 한 구간. 굴절 지점에서 끊긴다.</summary>
    public readonly struct Segment
    {
        public readonly Vector2 A;
        public readonly Vector2 B;

        public Segment(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }

        public float Length => (B - A).magnitude;
        public Vector2 Direction
        {
            get
            {
                Vector2 delta = B - A;
                return delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector2.down;
            }
        }
    }

    /// <summary>공중을 지나가는 물줄기는 캐릭터(10)보다 앞에 그린다.</summary>
    private const int BeamSortingOrder = 12;

    private class Strip
    {
        public SpriteRenderer Renderer;
        public Vector2 Origin;
        public Vector2 Direction;
        public float Length;
        /// <summary>선두가 지나가기 전에는 음수. 지나간 뒤 사라질 시각이 들어간다.</summary>
        public float ExpireAt = -1f;
    }

    private readonly List<Strip> strips = new List<Strip>(4);
    private List<Segment> path;
    private int segmentIndex;
    private float travelled;
    private bool headDone;

    private float speed;
    private float width;
    private float trailDuration;
    private int damage;
    private Color beamColor;
    private Color splashColor;

    private Transform player;
    private Health playerHealth;

    /// <summary>선두가 끝까지 갔고 남은 판정도 모두 사라졌는지. 다음 외부 패턴은 이걸 기다린다.</summary>
    public bool IsFinished { get; private set; }

    // ---------------------------------------------------------------- 경로 계산

    /// <summary>
    /// 발사 원점에서 시작해 수로 경계에서 <paramref name="refractions"/>번 꺾이는 경로를 만든다.
    /// 필요한 굴절을 채우기 전에 좌우 경계로 빠지면 <c>null</c>을 돌려준다 — 그런 후보는 버린다.
    /// </summary>
    /// <param name="laneSigns">수로별 방향. 인덱스는 <see cref="WaterCurrentField.LaneBottom"/> 계열 상수.</param>
    /// <param name="downward">위에서 아래로 쏘는지. 수로를 지나는 순서가 뒤집힌다.</param>
    /// <param name="crossings">채워 주면 경계 교차점을 순서대로 담는다. 디버그 로그용.</param>
    public static List<Segment> BuildPath(Vector2 origin, Vector2 firstDirection,
                                          Vector2 arenaCenter, Vector2 arenaHalf,
                                          int[] laneSigns, bool downward, int refractions,
                                          float refractionStrength, float epsilon,
                                          List<Vector2> crossings = null)
    {
        if (firstDirection.sqrMagnitude < 0.000001f) return null;

        // 위에서 쏘면 위 → 가운데 → 아래, 아래에서 쏘면 아래 → 가운데 → 위 순으로 지난다.
        int[] laneOrder = downward
            ? new[] { WaterCurrentField.LaneTop, WaterCurrentField.LaneMiddle, WaterCurrentField.LaneBottom }
            : new[] { WaterCurrentField.LaneBottom, WaterCurrentField.LaneMiddle, WaterCurrentField.LaneTop };
        // 지나는 순서대로의 수로 경계 Y.
        float third = arenaHalf.y / 3f;
        float[] boundaries = downward
            ? new[] { arenaCenter.y + third, arenaCenter.y - third }
            : new[] { arenaCenter.y - third, arenaCenter.y + third };

        List<Segment> result = new List<Segment>(refractions + 2);
        Vector2 position = origin;
        Vector2 direction = firstDirection.normalized;
        crossings?.Clear();

        int steps = Mathf.Clamp(refractions, 0, boundaries.Length);
        for (int i = 0; i < steps; i++)
        {
            if (!TryHorizontalHit(position, direction, boundaries[i], out Vector2 hit)) return null;
            // 굴절하기 전에 좌우로 빠져나가면 필요한 횟수를 채울 수 없다.
            if (Mathf.Abs(hit.x - arenaCenter.x) > arenaHalf.x) return null;

            result.Add(new Segment(position, hit));
            crossings?.Add(hit);

            // 새로 들어가는 수로의 방향을 적용한다. 수평 성분만 더하므로 위·아래 진행 부호는 그대로다.
            int sign = laneSigns[laneOrder[i + 1]];
            direction = (direction + Vector2.right * (sign * refractionStrength)).normalized;
            // 교차점에 그대로 두면 다음 계산이 같은 경계에 다시 걸려 제자리에서 떤다.
            position = hit + direction * epsilon;
        }

        Vector2 exit = ArenaExitPoint(position, direction, arenaCenter, arenaHalf);
        result.Add(new Segment(position, exit));
        return result;
    }

    /// <summary>주어진 Y선과 만나는 지점. 진행 방향이 그 선을 향하지 않으면 실패한다.</summary>
    private static bool TryHorizontalHit(Vector2 position, Vector2 direction, float y, out Vector2 hit)
    {
        hit = position;
        if (Mathf.Abs(direction.y) < 0.0001f) return false;
        float t = (y - position.y) / direction.y;
        if (t <= 0f) return false;
        hit = position + direction * t;
        return true;
    }

    /// <summary>전투 영역 바깥 경계와 만나는 지점. 물대포는 여기서 방향을 바꾸지 않고 사라진다.</summary>
    private static Vector2 ArenaExitPoint(Vector2 position, Vector2 direction,
                                          Vector2 arenaCenter, Vector2 arenaHalf)
    {
        float best = Mathf.Max(arenaHalf.x, arenaHalf.y) * 4f;
        if (Mathf.Abs(direction.x) > 0.0001f)
        {
            float edge = arenaCenter.x + Mathf.Sign(direction.x) * arenaHalf.x;
            float t = (edge - position.x) / direction.x;
            if (t > 0f) best = Mathf.Min(best, t);
        }
        if (Mathf.Abs(direction.y) > 0.0001f)
        {
            float edge = arenaCenter.y + Mathf.Sign(direction.y) * arenaHalf.y;
            float t = (edge - position.y) / direction.y;
            if (t > 0f) best = Mathf.Min(best, t);
        }
        return position + direction * best;
    }

    // ---------------------------------------------------------------- 발사

    public static GyaradosHydroBeam Launch(Transform parent, List<Segment> path, float speed, float width,
                                           float trailDuration, int damage, Color color, Color splashColor)
    {
        GameObject go = new GameObject("HydroBeam");
        go.transform.SetParent(parent, false);

        GyaradosHydroBeam beam = go.AddComponent<GyaradosHydroBeam>();
        beam.path = path;
        beam.speed = Mathf.Max(0.1f, speed);
        beam.width = Mathf.Max(0.05f, width);
        beam.trailDuration = Mathf.Max(0f, trailDuration);
        beam.damage = damage;
        beam.beamColor = color;
        beam.splashColor = splashColor;
        return beam;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
        }

        if (path == null || path.Count == 0)
        {
            IsFinished = true;
            Destroy(gameObject);
            return;
        }
        BeginSegment(0);
    }

    private void Update()
    {
        AdvanceHead();
        DamagePlayer();
        RetireStrips();

        if (headDone && strips.Count == 0 && !IsFinished)
        {
            IsFinished = true;
            Destroy(gameObject);
        }
    }

    /// <summary>선두가 경로를 따라 전진하며 지금 구간을 늘린다.</summary>
    private void AdvanceHead()
    {
        if (headDone) return;

        travelled += speed * Time.deltaTime;
        float segmentLength = path[segmentIndex].Length;

        while (travelled >= segmentLength)
        {
            FinishSegment(segmentLength);
            travelled -= segmentLength;
            segmentIndex++;
            if (segmentIndex >= path.Count)
            {
                headDone = true;
                return;
            }
            // 굴절 지점의 물보라. 새 경로를 알려 주는 연출이고 피해는 없다.
            SpawnSplash(path[segmentIndex].A);
            BeginSegment(segmentIndex);
            segmentLength = path[segmentIndex].Length;
        }

        SetStripLength(strips[strips.Count - 1], travelled);
    }

    private void BeginSegment(int index)
    {
        Segment segment = path[index];

        GameObject go = new GameObject("BeamStrip");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Square;
        sr.color = beamColor;
        sr.sortingOrder = BeamSortingOrder;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.right, segment.Direction);

        Strip strip = new Strip
        {
            Renderer = sr,
            Origin = segment.A,
            Direction = segment.Direction,
            Length = 0f,
        };
        strips.Add(strip);
        SetStripLength(strip, 0f);
    }

    private void FinishSegment(float fullLength)
    {
        Strip strip = strips[strips.Count - 1];
        SetStripLength(strip, fullLength);
        // 지나간 구간은 유지 시간 동안 피해 판정으로 남는다.
        strip.ExpireAt = Time.time + trailDuration;
    }

    private void SetStripLength(Strip strip, float length)
    {
        strip.Length = Mathf.Max(0f, length);
        if (strip.Renderer == null) return;
        strip.Renderer.transform.position = strip.Origin + strip.Direction * (strip.Length * 0.5f);
        strip.Renderer.transform.localScale = new Vector3(strip.Length, width, 1f);
    }

    private void SpawnSplash(Vector2 at)
    {
        AttackTelegraph splash = AttackTelegraph.CreateCircle(transform.parent, at, width * 0.9f, splashColor);
        splash.Hold(0.22f);
    }

    /// <summary>
    /// 살아 있는 모든 구간에 대해 플레이어가 물줄기 안에 있는지 본다.
    /// 여러 구간에 동시에 닿아도 플레이어의 공통 무적 시간이 연타를 막아 준다.
    /// </summary>
    private void DamagePlayer()
    {
        if (player == null || playerHealth == null) return;
        if (playerHealth.IsDead || playerHealth.IsInvincible || damage <= 0) return;

        Vector2 position = player.position;
        float halfWidth = width * 0.5f;

        for (int i = 0; i < strips.Count; i++)
        {
            Strip strip = strips[i];
            if (strip.Length <= 0.01f) continue;

            Vector2 offset = position - strip.Origin;
            float along = Vector2.Dot(offset, strip.Direction);
            if (along < 0f || along > strip.Length) continue;

            Vector2 perpendicular = new Vector2(-strip.Direction.y, strip.Direction.x);
            if (Mathf.Abs(Vector2.Dot(offset, perpendicular)) > halfWidth) continue;

            playerHealth.TakeDamage(damage);
            return;
        }
    }

    /// <summary>유지 시간이 다한 구간을 지운다. 사라지기 직전에는 옅어진다.</summary>
    private void RetireStrips()
    {
        for (int i = strips.Count - 1; i >= 0; i--)
        {
            Strip strip = strips[i];
            if (strip.ExpireAt < 0f) continue;

            float remaining = strip.ExpireAt - Time.time;
            if (remaining <= 0f)
            {
                if (strip.Renderer != null) Destroy(strip.Renderer.gameObject);
                strips.RemoveAt(i);
                continue;
            }

            if (strip.Renderer != null && trailDuration > 0f)
            {
                Color c = beamColor;
                c.a = beamColor.a * Mathf.Clamp01(remaining / trailDuration);
                strip.Renderer.color = c;
            }
        }
    }
}
