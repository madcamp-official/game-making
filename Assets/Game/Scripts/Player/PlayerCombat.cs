using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 기본 공격. 마우스 좌클릭으로 캐릭터 기준 마우스 방향(360도 자유각)에
/// 원형 판정을 만들어 적(EnemyController)의 Health에 피해를 준다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private int attackDamage = 3;
    [SerializeField] private float attackRange = 0.9f;
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private float attackCooldown = 0.5f;
    // Attack 클립 길이 (AnimData.xml 지속시간 합 28틱 / 60)
    [SerializeField] private float attackAnimDuration = 0.467f;
    [SerializeField] private GameObject attackEffectPrefab;

    private PlayerController controller;
    private PlayerAnimator playerAnimator;
    private Health health;
    private float lastAttackTime = -999f;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        health = GetComponent<Health>();
    }

    public void SetAttackDamage(int value)
    {
        attackDamage = value;
    }

    private void Update()
    {
        if (!controller.ControlEnabled || (health != null && health.IsDead)) return;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            Attack(GetMouseDirection());
        }
    }

    // 캐릭터 기준 마우스 커서 방향 (360도 자유각)
    private Vector2 GetMouseDirection()
    {
        Camera cam = Camera.main;
        if (cam == null || Mouse.current == null) return controller.FacingDirection;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 direction = (Vector2)mouseWorld - (Vector2)transform.position;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : controller.FacingDirection;
    }

    private void Attack(Vector2 direction)
    {
        // 캐릭터가 공격 방향을 바라보게 하고 (애니메이션은 가장 가까운 8방향 사용)
        controller.SetFacing(direction);
        if (playerAnimator != null)
            playerAnimator.PlayAttack(attackAnimDuration);

        Vector2 origin = (Vector2)transform.position + direction * attackRange;

        if (attackEffectPrefab != null)
        {
            GameObject fx = Instantiate(attackEffectPrefab, origin, Quaternion.identity);
            Destroy(fx, 0.15f);
        }
        else
        {
            StartCoroutine(DebugAttackFlash(origin));
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.GetComponentInParent<EnemyController>() == null) continue;
            Health enemyHealth = hit.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
                enemyHealth.TakeDamage(attackDamage);
        }
    }

    private static Sprite whiteSprite;

    // 이펙트 프리팹이 없을 때 공격 판정 위치를 잠깐 표시하는 임시 연출
    private IEnumerator DebugAttackFlash(Vector2 origin)
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        GameObject marker = new GameObject("AttackFlash");
        marker.transform.position = origin;
        SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        sr.sortingOrder = 50;
        marker.transform.localScale = Vector3.one * attackRadius * 2f;
        yield return new WaitForSeconds(0.1f);
        Destroy(marker);
    }
}
