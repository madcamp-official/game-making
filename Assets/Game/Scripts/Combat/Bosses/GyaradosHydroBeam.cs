using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 갸라도스의 반사 하이드로펌프. 외곽 바다의 네 방향 중 한 곳에서 들어와, 전투장 벽에 닿을 때마다
/// 입사각과 같은 각도로 튕겨 나간다.
///
/// 경로는 발사 전에 <see cref="BuildPath"/>로 한 번에 계산해 구간 목록으로 들고 있는다.
/// 물리 레이캐스트를 쓰지 않으므로 장식 콜라이더나 잉어킹, 플레이어 때문에 경로가 달라지지 않는다 —
/// 플레이어에게 보여 주는 것은 첫 방향뿐이고, 그 뒤는 "벽에서 똑같은 각도로 튕긴다"는 규칙 하나로
/// 읽을 수 있어야 하기 때문이다.
///
/// 선두가 지나간 구간은 정해진 시간 동안 피해 판정으로 남는다. 굵기와 판정은 반사 전후가 같다.
/// </summary>
public class GyaradosHydroBeam : MonoBehaviour
{
    /// <summary>물대포가 들어오는 네 발사 면.</summary>
    public enum HydroFace { Top, Bottom, Left, Right }

    /// <summary>물줄기 한 구간. 반사 지점에서 끊긴다.</summary>
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

    /// <summary>진행 방향이 축과 거의 나란하면 그 축의 벽은 영영 만나지 못한다.</summary>
    private const float DirectionEpsilon = 0.0001f;
    /// <summary>발사 면 안쪽으로 이만큼도 향하지 않는 후보는 벽을 스칠 뿐이라 버린다.</summary>
    private const float MinInwardDot = 0.1f;
    /// <summary>두 면까지의 거리가 이 안에서 같으면 모서리로 본다. 모서리 이중 반사는 허용하지 않는다.</summary>
    private const float CornerTolerance = 0.05f;

    /// <summary>
    /// 외곽 원점에서 <paramref name="face"/>로 들어와 벽에서 <paramref name="reflections"/>번 튕긴 뒤
    /// 다음 벽에서 끝나는 경로를 만든다. 렌더링·코루틴을 건드리지 않는 순수 함수라 네 발사 면과
    /// 모서리 사례를 따로 검증할 수 있다.
    ///
    /// 다음 세 가지 중 하나라도 어기면 <c>null</c>을 돌려준다. 그런 후보는 발사하지 않고 버린다.
    /// <list type="bullet">
    ///   <item>최초 방향이 선택한 면의 바깥을 향하거나 모서리를 스치기만 한다.</item>
    ///   <item>반사점이 모서리에서 <paramref name="cornerMargin"/>보다 가깝거나 두 면에 동시에 닿는다.</item>
    ///   <item>이어지는 두 접점 사이가 <paramref name="minSegmentLength"/>보다 짧다.</item>
    /// </list>
    /// </summary>
    /// <param name="entryPoint">선택한 면을 처음 통과하는 지점. 진입은 반사로 세지 않는다.</param>
    /// <param name="reflectionPoints">채워 주면 실제로 꺾인 지점을 순서대로 담는다. 디버그 로그용.</param>
    public static List<Segment> BuildPath(Vector2 origin, Vector2 firstDirection,
                                          Vector2 boundsCenter, Vector2 boundsHalf, HydroFace face,
                                          int reflections, float epsilon, float cornerMargin,
                                          float minSegmentLength, out Vector2 entryPoint,
                                          List<Vector2> reflectionPoints = null)
    {
        entryPoint = origin;
        reflectionPoints?.Clear();
        if (firstDirection.sqrMagnitude < 0.000001f) return null;

        Vector2 direction = firstDirection.normalized;
        // 최초 방향은 반드시 전투장 안쪽을 향해야 한다.
        if (Vector2.Dot(direction, InwardNormal(face)) < MinInwardDot) return null;
        if (!TryEnterFace(origin, direction, face, boundsCenter, boundsHalf, cornerMargin, out entryPoint))
            return null;

        // 첫 구간은 외곽 원점부터 첫 반사점까지 이어 그린다. 진입점에 시각적 틈이 생기면 안 된다.
        List<Vector2> contacts = new List<Vector2>(reflections + 3) { origin };
        Vector2 previous = entryPoint;
        Vector2 position = entryPoint + direction * epsilon;

        // 목표 반사 횟수만큼 꺾은 뒤, 다음 벽에 닿는 구간까지 포함하고 끝낸다.
        for (int i = 0; i <= reflections; i++)
        {
            bool last = i == reflections;
            if (!TryNextWall(position, direction, boundsCenter, boundsHalf,
                             last ? 0f : cornerMargin, out Vector2 hit, out bool vertical))
                return null;
            if (Vector2.Distance(hit, previous) < minSegmentLength) return null;

            contacts.Add(hit);
            previous = hit;
            if (last) break;

            // reflected = d - 2 * dot(d, n) * n — 축 정렬된 벽이라 닿은 축의 부호만 뒤집힌다.
            direction = vertical ? new Vector2(-direction.x, direction.y)
                                 : new Vector2(direction.x, -direction.y);
            reflectionPoints?.Add(hit);
            // 반사점에 그대로 두면 다음 계산이 같은 벽에 다시 걸려 제자리에서 떤다.
            position = hit + direction * epsilon;
        }

        List<Segment> result = new List<Segment>(contacts.Count - 1);
        for (int i = 1; i < contacts.Count; i++) result.Add(new Segment(contacts[i - 1], contacts[i]));
        return result;
    }

    /// <summary>그 면에서 전투장 안쪽을 가리키는 법선.</summary>
    public static Vector2 InwardNormal(HydroFace face)
    {
        switch (face)
        {
            case HydroFace.Top: return Vector2.down;
            case HydroFace.Bottom: return Vector2.up;
            case HydroFace.Left: return Vector2.right;
            default: return Vector2.left;
        }
    }

    /// <summary>
    /// 외곽 원점에서 선택한 면을 통과하는 진입점. 이 통과는 <b>진입</b>이지 반사가 아니다.
    /// 면의 끝자락을 스치기만 하는 후보는 여기서 걸러 낸다.
    /// </summary>
    private static bool TryEnterFace(Vector2 origin, Vector2 direction, HydroFace face,
                                     Vector2 center, Vector2 half, float cornerMargin, out Vector2 entry)
    {
        entry = origin;
        bool vertical = face == HydroFace.Left || face == HydroFace.Right;

        float plane = vertical ? center.x - InwardNormal(face).x * half.x
                               : center.y - InwardNormal(face).y * half.y;
        float along = vertical ? direction.x : direction.y;
        if (Mathf.Abs(along) < DirectionEpsilon) return false;

        float t = (plane - (vertical ? origin.x : origin.y)) / along;
        if (t <= 0f) return false;
        entry = origin + direction * t;

        float lateral = vertical ? Mathf.Abs(entry.y - center.y) : Mathf.Abs(entry.x - center.x);
        float limit = (vertical ? half.y : half.x) - cornerMargin;
        return limit > 0f && lateral <= limit;
    }

    /// <summary>
    /// 전투장 안에서 진행 방향으로 가장 먼저 만나는 벽 하나. 양의 거리 중 가까운 쪽만 고르고,
    /// 두 면까지의 거리가 같으면 모서리이므로 실패로 본다.
    /// </summary>
    /// <param name="vertical">닿은 벽이 왼쪽·오른쪽이면 참, 위·아래면 거짓.</param>
    private static bool TryNextWall(Vector2 position, Vector2 direction, Vector2 center, Vector2 half,
                                    float cornerMargin, out Vector2 hit, out bool vertical)
    {
        hit = position;
        vertical = false;

        float toVertical = AxisDistance(position.x, direction.x, center.x, half.x);
        float toHorizontal = AxisDistance(position.y, direction.y, center.y, half.y);
        if (float.IsPositiveInfinity(Mathf.Min(toVertical, toHorizontal))) return false;
        if (Mathf.Abs(toVertical - toHorizontal) < CornerTolerance) return false;

        vertical = toVertical < toHorizontal;
        hit = position + direction * (vertical ? toVertical : toHorizontal);

        // 반사점은 모서리에서 충분히 떨어져야 한다. 종료 벽에는 여유값을 요구하지 않는다.
        float lateral = vertical ? Mathf.Abs(hit.y - center.y) : Mathf.Abs(hit.x - center.x);
        float limit = (vertical ? half.y : half.x) - cornerMargin;
        return limit > 0f && lateral <= limit;
    }

    /// <summary>한 축의 벽까지 남은 거리. 그 축으로 나아가지 않으면 무한대다.</summary>
    private static float AxisDistance(float from, float delta, float center, float half)
    {
        if (Mathf.Abs(delta) < DirectionEpsilon) return float.PositiveInfinity;
        float t = (center + Mathf.Sign(delta) * half - from) / delta;
        return t > 0f ? t : float.PositiveInfinity;
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
            // 벽 반사점의 물보라. 새 경로를 알려 주는 연출이고 피해는 없다.
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
