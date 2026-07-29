using UnityEngine;

/// <summary>
/// 방 안쪽 바닥의 규격. 15개 방이 모두 같은 크기(안쪽 14×10 타일, 벽 안쪽 면이 ±7 · ±5)라
/// 한 곳에서 관리한다.
///
/// <b>공격을 배치할 때 쓰는 "전투 영역"은 반드시 여기까지 닿아야 한다.</b> 이 값보다 좁게
/// 잡으면 벽에 붙은 띠가 통째로 영구 안전지대가 된다 — 갸라도스 격류 압착(±6.2×±4.2),
/// 신뇽 해류(±5.8×±3.8), 버터플 독가루·코뿌리 스톤샤워(±6.2×±4.2)가 모두 같은 이유로
/// 벽에 붙기만 하면 무시할 수 있는 패턴이었다.
///
/// 몸이 놓일 자리(순간이동 목적지, 도망 목표)는 벽을 파고들면 안 되므로
/// <see cref="BodyMargin"/>만큼 안으로 들인다. 공격 판정에는 이 여유를 쓰지 않는다.
/// </summary>
public static class RoomArena
{
    /// <summary>방 중심에서 벽 안쪽 면까지의 거리.</summary>
    public static readonly Vector2 HalfSize = new Vector2(7f, 5f);

    /// <summary>몸이 벽에 겹치지 않게 두는 여유. 가장 덩치 큰 적(코뿌리 0.8×0.9)의 반너비보다 넉넉하다.</summary>
    public const float BodyMargin = 0.5f;

    /// <summary>몸을 놓아도 되는 범위. 순간이동·도망 목적지에 쓴다.</summary>
    public static Vector2 BodyHalfSize => HalfSize - Vector2.one * BodyMargin;

    /// <summary>
    /// 이 오브젝트가 속한 방의 중심. 적은 방 프리팹의 자식으로 놓이므로 부모 위치가 곧 방 중심이다.
    /// 부모가 없으면(단독 배치·테스트) 자기 자리를 중심으로 본다.
    /// </summary>
    public static Vector2 CenterOf(Transform self)
    {
        if (self == null) return Vector2.zero;
        return self.parent != null ? (Vector2)self.parent.position : (Vector2)self.position;
    }

    /// <summary>
    /// <paramref name="point"/>를 방 안으로 가둔다. <paramref name="margin"/>은 경계에서
    /// 안쪽으로 들일 거리 — 0이면 벽 안쪽 면까지 허용한다.
    /// </summary>
    public static Vector2 Clamp(Vector2 point, Vector2 center, float margin)
    {
        float halfX = Mathf.Max(0f, HalfSize.x - margin);
        float halfY = Mathf.Max(0f, HalfSize.y - margin);
        return new Vector2(
            Mathf.Clamp(point.x, center.x - halfX, center.x + halfX),
            Mathf.Clamp(point.y, center.y - halfY, center.y + halfY));
    }
}
