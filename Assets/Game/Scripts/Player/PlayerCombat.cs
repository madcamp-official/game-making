using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 공격. 마우스 조준(360도 자유각) 기준으로
/// 좌클릭 = 기본 공격 1(근거리), 우클릭 = 기본 공격 2(잎날가르기 투사체).
/// 공격 중에는 이동 속도가 감소한다. 수치는 모두 Inspector에서 조정한다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("기본 공격 1 — 근거리")]
    [SerializeField, Min(0)] private int meleeDamage = 12;
    [SerializeField, Min(0f)] private float meleeRange = 0.9f;
    [SerializeField, Min(0f)] private float meleeRadius = 0.6f;
    [SerializeField, Min(0f)] private float meleeCooldown = 0.5f;
    [SerializeField, Min(0f)] private float meleeKnockbackForce = 6f;
    [SerializeField] private GameObject attackEffectPrefab;

    [Header("기본 공격 2 — 잎날가르기")]
    [SerializeField, Min(0)] private int razorDamage = 8;
    [SerializeField, Min(0f)] private float razorCooldown = 0.5f;
    [SerializeField, Min(0f)] private float razorSpawnOffset = 0.55f;
    [Tooltip("탄퍼짐. 조준 방향을 기준으로 좌우로 흩어지는 전체 각도(도). 0이면 정확히 조준 방향으로 나간다.")]
    [SerializeField, Range(0f, 90f)] private float razorSpreadAngle = 8f;
    [SerializeField] private Projectile razorLeafPrefab;

    [Header("공통")]
    [SerializeField, Range(0f, 1f)] private float attackMoveSpeedMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float attackAnimDuration = 0.467f;

    private static readonly Collider2D[] hitBuffer = new Collider2D[16];
    private static readonly ContactFilter2D noFilter = ContactFilter2D.noFilter;
    // 한 적이 콜라이더를 여러 개 가져도 한 번만 타격하기 위한 목록 (매 공격마다 비운다)
    private static readonly List<Health> struckTargets = new List<Health>(8);
    private static Sprite whiteSprite;

    private PlayerController controller;
    private PlayerAnimator playerAnimator;
    private Health health;
    private Camera mainCamera;
    private SpriteRenderer flashMarker; // 재사용하는 공격 판정 표시
    private float lastMeleeTime = -999f;
    private float lastRazorTime = -999f;
    private float slowUntil = -999f;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        health = GetComponent<Health>();
    }

    /// <summary>진화 등으로 공격력을 바꾼다.</summary>
    public void SetDamages(int melee, int razor)
    {
        meleeDamage = melee;
        razorDamage = razor;
    }

    private void Update()
    {
        // 공격 중 이동 감속 적용/해제
        controller.SpeedMultiplier = Time.time < slowUntil ? attackMoveSpeedMultiplier : 1f;

        if (!controller.ControlEnabled || (health != null && health.IsDead)) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && Time.time >= lastMeleeTime + meleeCooldown)
        {
            lastMeleeTime = Time.time;
            MeleeAttack(GetMouseDirection());
        }
        else if (mouse.rightButton.wasPressedThisFrame && Time.time >= lastRazorTime + razorCooldown)
        {
            lastRazorTime = Time.time;
            RazorLeafAttack(GetMouseDirection());
        }
    }

    // 캐릭터 기준 마우스 커서 방향 (360도 자유각)
    private Vector2 GetMouseDirection()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || Mouse.current == null) return controller.FacingDirection;
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 direction = (Vector2)mouseWorld - (Vector2)transform.position;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : controller.FacingDirection;
    }

    private void BeginAttack(Vector2 direction)
    {
        controller.SetFacing(direction);
        slowUntil = Time.time + attackAnimDuration;
        if (playerAnimator != null)
            playerAnimator.PlayAttack(attackAnimDuration);
    }

    private void MeleeAttack(Vector2 direction)
    {
        BeginAttack(direction);
        Vector2 origin = (Vector2)transform.position + direction * meleeRange;

        if (attackEffectPrefab != null)
        {
            GameObject fx = Instantiate(attackEffectPrefab, origin, Quaternion.identity);
            Destroy(fx, 0.15f);
        }
        else
        {
            StartCoroutine(DebugAttackFlash(origin));
        }

        struckTargets.Clear();
        int count = Physics2D.OverlapCircle(origin, meleeRadius, noFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (struckTargets.Contains(enemyHealth)) continue;
            struckTargets.Add(enemyHealth);

            enemyHealth.TakeDamage(meleeDamage);
            enemy.ApplyKnockback(direction, meleeKnockbackForce);
        }
    }

    private void RazorLeafAttack(Vector2 direction)
    {
        BeginAttack(direction); // 캐릭터는 조준 방향 그대로 바라본다

        if (razorLeafPrefab == null) return;

        Vector2 shotDirection = ApplySpread(direction, razorSpreadAngle);
        Vector2 spawnPos = (Vector2)transform.position + shotDirection * razorSpawnOffset;
        Projectile leaf = Instantiate(razorLeafPrefab, spawnPos, Quaternion.identity);
        leaf.Launch(shotDirection, razorDamage);
    }

    /// <summary>탄퍼짐: 조준 방향을 전체 각도 범위 안에서 무작위로 틀어 준다.</summary>
    private static Vector2 ApplySpread(Vector2 direction, float spreadAngle)
    {
        if (spreadAngle <= 0f) return direction;
        float half = spreadAngle * 0.5f;
        float offset = Random.Range(-half, half) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(offset);
        float sin = Mathf.Sin(offset);
        return new Vector2(direction.x * cos - direction.y * sin,
                           direction.x * sin + direction.y * cos);
    }

    private static readonly WaitForSeconds flashDuration = new WaitForSeconds(0.1f);

    // 이펙트 프리팹이 없을 때 공격 판정 위치를 잠깐 표시하는 임시 연출.
    // 마커 오브젝트는 한 번만 만들어 재사용한다.
    private IEnumerator DebugAttackFlash(Vector2 origin)
    {
        if (flashMarker == null)
        {
            if (whiteSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            GameObject marker = new GameObject("AttackFlash");
            flashMarker = marker.AddComponent<SpriteRenderer>();
            flashMarker.sprite = whiteSprite;
            flashMarker.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            flashMarker.sortingOrder = 50;
        }

        flashMarker.transform.position = origin;
        flashMarker.transform.localScale = Vector3.one * meleeRadius * 2f;
        flashMarker.enabled = true;
        yield return flashDuration;
        if (flashMarker != null) flashMarker.enabled = false;
    }
}
