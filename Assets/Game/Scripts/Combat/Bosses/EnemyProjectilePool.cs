using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 투사체 풀. 전투 시작 전에 필요한 수만큼 미리 만들어 두고 빌려 쓴다.
///
/// 강화 은빛바람은 한 패턴에 최대 96발을 순차 발사한다. 발사할 때마다 <see cref="GameObject"/>와
/// <see cref="Rigidbody2D"/>, <see cref="Collider2D"/>, 시각 자식을 새로 만들면
/// 반복 전투에서 프레임이 끊기므로, 준비 단계에서 전부 만들어 두고 값만 갈아 끼운다.
///
/// 풀이 비면 새로 만들지 않고 그 발사를 건너뛴다. 그래야 활성 투사체 수가
/// 정해진 상한을 넘지 않는다.
/// </summary>
public class EnemyProjectilePool : MonoBehaviour
{
    private readonly Stack<EnemyProjectile> idle = new Stack<EnemyProjectile>();
    private readonly List<EnemyProjectile> all = new List<EnemyProjectile>();

    private bool warnedEmpty;

    /// <summary>투사체가 이 범위를 벗어나면 스스로 반환한다.</summary>
    public Vector2 ArenaCenter { get; private set; }
    public Vector2 ArenaHalfSize { get; private set; } = new Vector2(100f, 100f);

    public int Capacity => all.Count;
    public int ActiveCount => all.Count - idle.Count;

    /// <summary>
    /// 풀을 만들고 투사체를 미리 생성한다. <paramref name="parent"/>는 배율이 1이어야
    /// 콜라이더 반지름이 의도한 크기로 유지된다.
    /// </summary>
    public static EnemyProjectilePool Create(Transform parent, int capacity)
    {
        GameObject go = new GameObject("EnemyProjectilePool");
        go.transform.SetParent(parent, false);

        EnemyProjectilePool pool = go.AddComponent<EnemyProjectilePool>();
        for (int i = 0; i < Mathf.Max(0, capacity); i++)
        {
            EnemyProjectile projectile = EnemyProjectile.CreatePooled(go.transform, pool);
            pool.all.Add(projectile);
            pool.idle.Push(projectile);
        }
        return pool;
    }

    public void SetArena(Vector2 center, Vector2 halfSize)
    {
        ArenaCenter = center;
        // 경기장을 살짝 넘어가도 바로 사라지지 않게 여유를 둔다.
        ArenaHalfSize = halfSize + Vector2.one * 2f;
    }

    /// <summary>비어 있는 투사체를 하나 꺼낸다. 남은 게 없으면 null.</summary>
    public EnemyProjectile Borrow()
    {
        if (idle.Count == 0)
        {
            // 풀 고갈은 수치 설정 문제라 한 번만 알리면 충분하다.
            if (!warnedEmpty)
            {
                warnedEmpty = true;
                Debug.LogWarning("[EnemyProjectilePool] 투사체가 부족해 발사를 건너뛴다. " +
                                 "용량 " + Capacity + "개를 늘리거나 패턴 탄수를 줄여야 한다.", this);
            }
            return null;
        }
        return idle.Pop();
    }

    /// <summary>투사체가 스스로 호출한다. 직접 부를 일은 없다.</summary>
    public void Return(EnemyProjectile projectile)
    {
        if (projectile == null || idle.Contains(projectile)) return;
        idle.Push(projectile);
    }

    /// <summary>날아다니던 투사체를 전부 회수한다. 보스 사망·페이즈 전환에서 쓴다.</summary>
    public void ReturnAll()
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null) all[i].Deactivate();
    }
}
