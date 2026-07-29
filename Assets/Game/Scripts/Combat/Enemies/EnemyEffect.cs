using UnityEngine;

/// <summary>
/// 적이 방에 남긴 것들(장판·예고·날아가던 물체)의 표식.
///
/// 이런 오브젝트는 시전한 적이 아니라 <b>방</b>에 붙는다(<see cref="EnemyAbility.EffectRoot"/>).
/// 적이 죽는 순간 공중의 뼈다귀나 바닥의 독장판이 함께 사라지면, 이미 나간 공격이
/// 없던 일이 되기 때문이다. 그 대가로 마지막 적을 잡은 뒤에도 한동안 방에 남아
/// 빈 방에서 플레이어를 때린다. 싸움이 끝나면 <see cref="ClearUnder"/>로 한꺼번에 걷어 낸다.
///
/// 표식을 따로 둔 이유: 걷어 낼 것들이 <see cref="DamageZone"/>·<see cref="AttackTelegraph"/>처럼
/// 서로 다른 컴포넌트이거나, 아예 컴포넌트 없는 스프라이트(텅구리의 뼈, 아쿠스타의 광선)라
/// 형(型) 하나로는 모을 수가 없다.
/// </summary>
public class EnemyEffect : MonoBehaviour
{
    /// <summary>표식을 붙이고 그대로 돌려준다. 만드는 쪽에서 한 줄로 감싸 쓴다.</summary>
    public static GameObject Mark(GameObject go)
    {
        if (go != null && go.GetComponent<EnemyEffect>() == null) go.AddComponent<EnemyEffect>();
        return go;
    }

    /// <summary>
    /// <paramref name="root"/> 아래에 남은 적의 흔적을 전부 치운다.
    /// 비활성 오브젝트까지 훑는다 — 풀에 돌아가 꺼져 있는 탄도 세어야 하기 때문이다.
    /// </summary>
    public static void ClearUnder(Transform root)
    {
        if (root == null) return;

        // 탄은 풀에서 빌린 것이라 지우면 풀이 비어 버린다. 제자리로 돌려보내기만 한다.
        foreach (EnemyProjectile projectile in root.GetComponentsInChildren<EnemyProjectile>(true))
            if (projectile != null) projectile.Deactivate();

        foreach (EnemyEffect effect in root.GetComponentsInChildren<EnemyEffect>(true))
            if (effect != null) Destroy(effect.gameObject);
    }
}
