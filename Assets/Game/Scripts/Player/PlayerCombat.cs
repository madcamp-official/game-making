using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어 공격. 마우스 조준(360도 자유각) 기준으로
/// 좌클릭 = 기본 공격 1(근거리), 우클릭 = 기본 공격 2(덩굴채찍).
/// 공격 중에는 이동 속도가 감소하고, 덩굴채찍은 휘두른 뒤 짧게 경직이 걸린다.
/// 수치는 모두 Inspector에서 조정한다.
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

    [Header("기본 공격 2 — 덩굴채찍")]
    [FormerlySerializedAs("razorDamage")]
    [SerializeField, Min(0)] private int vineDamage = 8;
    [FormerlySerializedAs("razorCooldown")]
    [SerializeField, Min(0f)] private float vineCooldown = 5f;
    [Tooltip("채찍이 닿는 거리. 타일 한 칸이 1이다.")]
    [SerializeField, Min(0f)] private float vineRange = 2f;
    [Tooltip("채찍 판정의 굵기.")]
    [SerializeField, Min(0f)] private float vineWidth = 0.7f;
    [SerializeField, Min(0f)] private float vineKnockbackForce = 4f;
    [Tooltip("휘두른 뒤 움직이지 못하는 시간.")]
    [SerializeField, Min(0f)] private float vineStunDuration = 0.5f;
    [SerializeField] private Color vineColor = new Color(0.3f, 0.85f, 0.25f, 0.95f);

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
    private PlayerMoves moves;
    private Health health;
    private Camera mainCamera;
    private SpriteRenderer flashMarker; // 재사용하는 공격 판정 표시
    private SpriteRenderer vineMarker;  // 재사용하는 덩굴채찍 연출
    private float lastMeleeTime = -999f;
    private float lastVineTime = -999f;
    private float slowUntil = -999f;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        moves = GetComponent<PlayerMoves>();
        health = GetComponent<Health>();
    }

    // ---------------------------------------------------------------- 강화가 반영된 수치

    private float EffectiveMeleeCooldown =>
        meleeCooldown * (moves != null ? moves.TackleCooldownMultiplier : 1f);

    /// <summary>공격 중 이동 배율. 강화는 "느려지는 정도"를 깎는 것이라 1에서 뺀 값에 곱한다.</summary>
    private float EffectiveAttackMoveSpeedMultiplier
    {
        get
        {
            float reduction = 1f - attackMoveSpeedMultiplier;
            if (moves != null) reduction *= moves.TackleSlowReductionMultiplier;
            return Mathf.Clamp01(1f - reduction);
        }
    }

    private float EffectiveVineCooldown =>
        vineCooldown * (moves != null ? moves.VineCooldownMultiplier : 1f);
    private float EffectiveVineRange =>
        vineRange * (moves != null ? moves.VineRangeMultiplier : 1f);
    private float EffectiveVineStun =>
        vineStunDuration * (moves != null ? moves.VineStunMultiplier : 1f);

    /// <summary>
    /// 기술 칸 HUD가 쓰는 쿨타임 진행도. 1이면 바로 쓸 수 있고, 0이면 방금 썼다.
    /// 쿨타임이 없는 기술은 항상 1이다.
    /// </summary>
    public float CooldownProgress01(MoveType move)
    {
        float last, cooldown;
        switch (move)
        {
            case MoveType.Tackle: last = lastMeleeTime; cooldown = EffectiveMeleeCooldown; break;
            case MoveType.VineWhip: last = lastVineTime; cooldown = EffectiveVineCooldown; break;
            default: return 1f;   // 광합성·꽃잎댄스는 아직 구현하지 않았다
        }
        if (cooldown <= 0f) return 1f;
        return Mathf.Clamp01((Time.time - last) / cooldown);
    }

    /// <summary>진화 등으로 기본 공격력을 바꾼다. 유물 배율은 공격할 때 따로 곱해진다.</summary>
    public void SetDamages(int melee, int vine)
    {
        meleeDamage = melee;
        vineDamage = vine;
    }

    // 유물 배율이 걸린 실제 피해량. 배율이 아무리 낮아도 최소 1은 들어간다.
    private int EffectiveMeleeDamage =>
        ScaleDamage(meleeDamage, RelicMultiplier(true) *
            (moves != null ? moves.TackleDamageMultiplier : 1f));
    // 덩굴채찍은 사거리가 2칸이라 몸으로 붙는 좌클릭과 역할이 다르다. 구애 시리즈에서는
    // 잎날가르기가 있던 자리를 그대로 이어받아 "원거리" 쪽 배율을 쓴다.
    private int EffectiveVineDamage => ScaleDamage(vineDamage, RelicMultiplier(false));

    private static int ScaleDamage(int baseDamage, float multiplier) =>
        baseDamage <= 0 ? 0 : Mathf.Max(1, GameMath.RoundHalfUp(baseDamage * multiplier));

    private static float RelicMultiplier(bool melee)
    {
        RelicManager relics = RelicManager.Instance;
        if (relics == null) return 1f;
        return melee ? relics.MeleeDamageMultiplier : relics.RangedDamageMultiplier;
    }

    private void Update()
    {
        // 공격 중 이동 감속 적용/해제
        controller.SpeedMultiplier = Time.time < slowUntil ? EffectiveAttackMoveSpeedMultiplier : 1f;

        if (!controller.ControlEnabled || (health != null && health.IsDead)) return;
        // 경직 중에는 공격도 못 한다. 후딜이 없는 것과 같아지면 경직을 넣은 의미가 없다.
        if (controller.IsStunned) return;
        // 강화 팔레트가 떠 있는 동안에는 클릭이 공격으로 새면 안 된다.
        if (MoveUpgradePanel.IsOpen) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && CanUse(MoveType.Tackle))
        {
            lastMeleeTime = Time.time;
            MeleeAttack(GetMouseDirection());
        }
        else if (mouse.rightButton.wasPressedThisFrame && CanUse(MoveType.VineWhip))
        {
            lastVineTime = Time.time;
            VineWhipAttack(GetMouseDirection());
        }
        // 세 번째·네 번째 기술은 칸과 조작키만 잡아 두고 아직 아무 일도 하지 않는다.
    }

    /// <summary>배운 기술이고 쿨타임도 끝났는지.</summary>
    private bool CanUse(MoveType move)
    {
        if (moves != null && !moves.Has(move)) return false;
        return CooldownProgress01(move) >= 1f;
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

    /// <summary>근접 공격용. 조준 방향을 보고, 공격 모션을 재생하며 그동안 감속한다.</summary>
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

        int damage = EffectiveMeleeDamage;
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

            enemyHealth.TakeDamage(damage);
            enemy.ApplyKnockback(direction, meleeKnockbackForce);
            PlayerRelicEffects.ReportDamageDealt(damage);
        }
    }

    /// <summary>
    /// 덩굴채찍. 조준 방향으로 2칸 길이의 초록 채찍을 뻗어, 그 선 위에 닿은 적을 전부 때린다.
    /// 휘두른 뒤에는 짧게 경직이 걸려 바로 도망칠 수 없다 — 사거리를 준 대신 붙은 대가다.
    /// </summary>
    private void VineWhipAttack(Vector2 direction)
    {
        // 근접 공격과 달리 공격 모션을 재생하지 않는다. 조준 방향만 바라보고,
        // 감속도 걸지 않는다 — 감속은 공격 모션 길이에 묶인 값인데 모션이 없고,
        // 어차피 경직 동안 못 움직인다.
        controller.SetFacing(direction);
        controller.Stun(EffectiveVineStun);

        // 판정은 플레이어 앞으로 뻗은 직사각형이다. 그린 채찍과 같은 범위여야 한다.
        float range = EffectiveVineRange;
        Vector2 origin = transform.position;
        Vector2 center = origin + direction * (range * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        StartCoroutine(VineWhipFlash(origin, direction, range));

        int damage = EffectiveVineDamage;
        struckTargets.Clear();
        int count = Physics2D.OverlapBox(center, new Vector2(range, vineWidth), angle,
                                         noFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = hitBuffer[i].GetComponentInParent<EnemyController>();
            if (enemy == null) continue;
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (struckTargets.Contains(enemyHealth)) continue;
            struckTargets.Add(enemyHealth);

            enemyHealth.TakeDamage(damage);
            enemy.ApplyKnockback(direction, vineKnockbackForce);
            PlayerRelicEffects.ReportDamageDealt(damage);
        }
    }

    /// <summary>채찍이 뻗었다가 사라지는 연출. 마커 하나를 계속 재사용한다.</summary>
    private IEnumerator VineWhipFlash(Vector2 origin, Vector2 direction, float range)
    {
        if (vineMarker == null)
        {
            EnsureWhiteSprite();
            GameObject marker = new GameObject("VineWhip");
            vineMarker = marker.AddComponent<SpriteRenderer>();
            vineMarker.sprite = whiteSprite;
            vineMarker.sortingOrder = 50;
        }

        // 스프라이트 중심이 아니라 뿌리(플레이어 쪽)를 기준으로 늘어나야 채찍처럼 보인다.
        Transform t = vineMarker.transform;
        t.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        vineMarker.color = vineColor;
        vineMarker.enabled = true;

        const float ExtendTime = 0.07f;
        const float HoldTime = 0.05f;

        float elapsed = 0f;
        while (elapsed < ExtendTime)
        {
            elapsed += Time.deltaTime;
            float length = range * Mathf.Clamp01(elapsed / ExtendTime);
            t.position = origin + direction * (length * 0.5f);
            t.localScale = new Vector3(length, vineWidth, 1f);
            yield return null;
        }

        t.position = origin + direction * (range * 0.5f);
        t.localScale = new Vector3(range, vineWidth, 1f);
        yield return new WaitForSeconds(HoldTime);
        if (vineMarker != null) vineMarker.enabled = false;
    }

    private static readonly WaitForSeconds flashDuration = new WaitForSeconds(0.1f);

    private static void EnsureWhiteSprite()
    {
        if (whiteSprite != null) return;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    // 이펙트 프리팹이 없을 때 공격 판정 위치를 잠깐 표시하는 임시 연출.
    // 마커 오브젝트는 한 번만 만들어 재사용한다.
    private IEnumerator DebugAttackFlash(Vector2 origin)
    {
        if (flashMarker == null)
        {
            EnsureWhiteSprite();
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
