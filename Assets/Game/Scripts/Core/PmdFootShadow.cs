using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PMD 원작풍 발밑 그림자. SpriteCollab의 애니메이션별 Shadow 시트에서 잘라 온
/// 그림자 스프라이트를, 본체가 지금 보여 주는 프레임과 짝지어 따라 그린다.
///
/// 그림자 시트는 본체 시트와 프레임 격자·중심이 같아서, 본체와 같은 위치에 그리면
/// 발밑의 올바른 자리(걷기 동작의 들썩임 포함)에 정확히 놓인다. 짝은 이름이 아니라
/// 스프라이트 참조로 맺는다 — 주인공처럼 진화하며 종이 바뀌어도(이상해씨·이상해풀·
/// 이상해꽃 시트가 이름은 같다) 헷갈리지 않는다.
///
/// 렌더러는 Awake에서 자식으로 만들어 붙인다. 프리팹에는 이 컴포넌트 하나와
/// 짝 목록만 있으면 된다.
/// </summary>
public class PmdFootShadow : MonoBehaviour
{
    [Tooltip("그림자를 따라 그릴 본체 렌더러.")]
    [SerializeField] private SpriteRenderer owner;
    [Tooltip("본체 프레임. shadowSprites와 같은 인덱스가 짝이다.")]
    [SerializeField] private Sprite[] bodySprites;
    [Tooltip("그림자 프레임(흰색 마스크). 색은 shadowColor로 입힌다.")]
    [SerializeField] private Sprite[] shadowSprites;
    [SerializeField] private Color shadowColor = new Color(0.04f, 0.09f, 0.2f, 0.42f);
    [Tooltip("본체 정렬 순서에 더하는 값. 음수라 본체 바로 아래에 깔린다.")]
    [SerializeField] private int sortingOffset = -1;

    private SpriteRenderer shadowRenderer;
    private readonly Dictionary<Sprite, Sprite> shadowOf = new Dictionary<Sprite, Sprite>();

    private void Awake()
    {
        if (owner == null) owner = GetComponent<SpriteRenderer>();
        int count = Mathf.Min(bodySprites != null ? bodySprites.Length : 0,
                              shadowSprites != null ? shadowSprites.Length : 0);
        for (int i = 0; i < count; i++)
            if (bodySprites[i] != null && shadowSprites[i] != null)
                shadowOf[bodySprites[i]] = shadowSprites[i];

        GameObject go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        shadowRenderer = go.AddComponent<SpriteRenderer>();
        shadowRenderer.color = shadowColor;
        if (owner != null)
        {
            shadowRenderer.sortingLayerID = owner.sortingLayerID;
            shadowRenderer.sortingOrder = owner.sortingOrder + sortingOffset;
        }
    }

    private void LateUpdate()
    {
        if (owner == null || shadowRenderer == null) return;

        Sprite body = owner.sprite;
        bool visible = owner.enabled && body != null && shadowOf.TryGetValue(body, out Sprite shadow);
        shadowRenderer.enabled = visible;
        if (!visible) return;

        shadowRenderer.sprite = shadowOf[body];
        shadowRenderer.flipX = owner.flipX;
        shadowRenderer.flipY = owner.flipY;

        // 본체가 흐려지면(닥트리오 잠수 등) 그림자도 함께 옅어진다. 정렬 순서 변화도 따라간다.
        Color c = shadowColor;
        c.a *= owner.color.a;
        shadowRenderer.color = c;
        shadowRenderer.sortingOrder = owner.sortingOrder + sortingOffset;
    }
}
