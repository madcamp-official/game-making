using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어의 네 기술. 마우스 조준(360도 자유각) 기준이다.
///
/// * 좌클릭 — 몸통박치기 (근접)
/// * 우클릭 — 덩굴채찍 (원거리, 2칸 사거리, 휘두른 뒤 짧은 경직)
/// * 좌측 Shift — 씨뿌리기 (발밑에 회복 장판, 전투방마다 한 번)
/// * Space — 꽃잎댄스 (몸을 따라다니는 피해 장판, 근접)
///
/// 피해를 주는 기술에는 사거리 속성이 붙어 있다 (<see cref="MoveInfo.KindOf"/>).
/// 유물과 이벤트 강화는 그 속성만 보고 배율을 매긴다.
///
/// 기술은 전투방과 보스방에서만 쓸 수 있다 (<see cref="MovesUsable"/>).
/// 수치는 모두 Inspector에서 조정한다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("기본 공격 1 — 근거리")]
    [SerializeField, Min(0)] private int meleeDamage = 12;
    [SerializeField, Min(0f)] private float meleeRange = 0.9f;
    [Tooltip("휘두르는 원의 반지름. 0.6에서 넓이가 두 배가 되도록 √2를 곱했다 — " +
             "몰려드는 잡몹을 한 번에 쓸어야 근접이 근접다워진다.")]
    [SerializeField, Min(0f)] private float meleeRadius = 0.85f;
    [SerializeField, Min(0f)] private float meleeCooldown = 0.5f;
    [SerializeField, Min(0f)] private float meleeKnockbackForce = 6f;
    [SerializeField] private GameObject attackEffectPrefab;

    [Header("기본 공격 2 — 덩굴채찍 (원거리 견제)")]
    // 피해량은 진화 단계가 덮어쓴다(PlayerEvolution.stages). 여기 값은 1단계와 같게 맞춰 둔다.
    //
    // 덩굴채찍은 딜을 담당하지 않는다. 값어치는 사거리·넉백·경직에 있고, 피해는 몸통박치기의
    // 4할쯤에 묶어 둔다. 예전에는 한 대가 몸통박치기와 맞먹어(14 대 12) 붙지 않고 채찍만
    // 휘두르는 편이 이득인 구간이 있었다 — 근접 포켓몬이 근접할 이유가 없어졌다.
    [FormerlySerializedAs("razorDamage")]
    [SerializeField, Min(0)] private int vineDamage = 5;
    [FormerlySerializedAs("razorCooldown")]
    [SerializeField, Min(0f)] private float vineCooldown = 2.2f;
    [Tooltip("채찍이 닿는 거리. 타일 한 칸이 1이다.")]
    [SerializeField, Min(0f)] private float vineRange = 2.8f;
    [Tooltip("채찍 판정의 굵기.")]
    [SerializeField, Min(0f)] private float vineWidth = 0.9f;
    [SerializeField, Min(0f)] private float vineKnockbackForce = 5f;
    [Tooltip("휘두른 뒤 움직이지 못하는 시간.")]
    [SerializeField, Min(0f)] private float vineStunDuration = 0.25f;
    [SerializeField] private Color vineColor = new Color(0.3f, 0.85f, 0.25f, 0.95f);

    [Header("기술 3 — 씨뿌리기")]
    [Tooltip("발밑에 까는 회복 장판의 반지름.")]
    [SerializeField, Min(0f)] private float seedRadius = 2f;
    [SerializeField, Min(0f)] private float seedDuration = 5f;
    [Tooltip("장판 위에 서 있는 동안 한 번에 차오르는 체력.")]
    [SerializeField, Min(0)] private int seedHealPerTick = 6;
    [SerializeField, Min(0.05f)] private float seedTickInterval = 1f;
    // 1층 숲 바닥이 초록이라 흔한 초록으로는 묻힌다. 밝은 연둣빛으로 띄운다.
    [SerializeField] private Color seedColor = new Color(0.6f, 1f, 0.45f, 0.4f);

    [Header("기술 4 — 꽃잎댄스 (원거리)")]
    [SerializeField, Min(0f)] private float petalRadius = 1.8f;
    [SerializeField, Min(0f)] private float petalDuration = 2.5f;
    [Tooltip("한 틱의 피해량이 몸통박치기 기본 피해의 몇 배인지. 꽃잎댄스는 원거리 기술이지만 " +
             "기준값은 몸통박치기를 쓴다 — 덩굴채찍은 견제기라 피해가 낮아 기준으로 삼을 수 없다.")]
    [SerializeField, Min(0f)] private float petalDamageRatio = 1f;
    [SerializeField, Min(0.05f)] private float petalTickInterval = 0.5f;
    [SerializeField, Min(0f)] private float petalCooldown = 12f;
    [SerializeField] private Color petalColor = new Color(1f, 0.45f, 0.75f, 0.38f);

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
    private float lastPetalTime = -999f;
    /// <summary>씨뿌리기를 쓴 전투방의 번호. 방이 바뀌면 다시 쓸 수 있다.</summary>
    private int seedUsedInRoom = -1;
    private float slowUntil = -999f;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        moves = GetComponent<PlayerMoves>();
        health = GetComponent<Health>();
    }

    // ---------------------------------------------------------------- 강화가 반영된 수치

    // 유물이 거는 배율. 유물이 아직 없으면 1이다.
    // 시간으로 도는 쿨타임에만 걸린다 — 씨뿌리기는 방을 넘어가야 돌아오므로 선제공격손톱이 닿지 않는다.
    private static float RelicCooldownMultiplier =>
        RelicManager.Instance != null ? RelicManager.Instance.CooldownMultiplier : 1f;
    private static float RelicAttackSizeMultiplier =>
        RelicManager.Instance != null ? RelicManager.Instance.AttackSizeMultiplier : 1f;
    private static float RelicZoneDurationMultiplier =>
        RelicManager.Instance != null ? RelicManager.Instance.ZoneDurationMultiplier : 1f;

    private float EffectiveMeleeCooldown =>
        meleeCooldown * (moves != null ? moves.TackleCooldownMultiplier : 1f) * RelicCooldownMultiplier;

    /// <summary>몸통박치기 판정 원의 반지름. 광각렌즈가 키운다.</summary>
    private float EffectiveMeleeRadius => meleeRadius * RelicAttackSizeMultiplier;

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
        vineCooldown * (moves != null ? moves.VineCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectiveVineRange =>
        vineRange * (moves != null ? moves.VineRangeMultiplier : 1f) * RelicAttackSizeMultiplier;
    /// <summary>채찍 판정의 굵기. 길이와 함께 광각렌즈를 탄다.</summary>
    private float EffectiveVineWidth => vineWidth * RelicAttackSizeMultiplier;
    private float EffectiveVineStun =>
        vineStunDuration * (moves != null ? moves.VineStunMultiplier : 1f);

    // 씨뿌리기는 공격이 아니라 회복 장판이라 광각렌즈가 닿지 않는다. 지속시간만 빛의점토를 탄다.
    private float EffectiveSeedRadius =>
        seedRadius * (moves != null ? moves.SeedRadiusMultiplier : 1f);
    private float EffectiveSeedDuration =>
        (seedDuration + (moves != null ? moves.SeedDurationBonus : 0f)) * RelicZoneDurationMultiplier;
    private int EffectiveSeedHeal =>
        seedHealPerTick + (moves != null ? moves.SeedHealBonus : 0);

    private float EffectivePetalCooldown => petalCooldown * RelicCooldownMultiplier;
    private float EffectivePetalRadius =>
        petalRadius * (moves != null ? moves.PetalRadiusMultiplier : 1f) * RelicAttackSizeMultiplier;
    private float EffectivePetalDuration =>
        (petalDuration + (moves != null ? moves.PetalDurationBonus : 0f)) * RelicZoneDurationMultiplier;

    /// <summary>
    /// 꽃잎댄스 한 틱의 피해. 몸통박치기의 <b>기본</b> 피해가 기준이라, 몸통박치기를 강화해도
    /// 이쪽이 같이 세지지는 않는다 — 한 기술의 강화가 다른 기술로 새면 선택의 뜻이 사라진다.
    /// 배율은 기술에 붙은 속성(근접)을 따른다.
    /// </summary>
    private int EffectivePetalDamage =>
        ScaleDamage(meleeDamage, petalDamageRatio * KindMultiplier(MoveType.PetalDance) *
            (moves != null ? moves.PetalDamageMultiplier : 1f));

    /// <summary>
    /// 씨뿌리기는 전투방마다 한 번만 쓸 수 있다. 시간이 지나 돌아오는 게 아니라
    /// 방을 넘어가야 돌아오므로, "이 방에서 언제 쓸 것인가"가 곧 선택이 된다.
    /// </summary>
    private bool SeedReady =>
        CombatRoomController.InCombatRoom && seedUsedInRoom != CombatRoomController.VisitId;

    /// <summary>
    /// 기술 칸 HUD가 쓰는 쿨타임 진행도. 1이면 바로 쓸 수 있고, 0이면 방금 썼다.
    /// 쿨타임이 없는 기술은 항상 1이다.
    /// </summary>
    public float CooldownProgress01(MoveType move)
    {
        // 씨뿌리기는 시간으로 차지 않는다. 이 방에서 썼으면 방을 나갈 때까지 계속 0이다.
        if (move == MoveType.SeedSow) return SeedReady ? 1f : 0f;

        float last, cooldown;
        switch (move)
        {
            case MoveType.Tackle: last = lastMeleeTime; cooldown = EffectiveMeleeCooldown; break;
            case MoveType.VineWhip: last = lastVineTime; cooldown = EffectiveVineCooldown; break;
            case MoveType.PetalDance: last = lastPetalTime; cooldown = EffectivePetalCooldown; break;
            default: return 1f;
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

    // 속성 배율이 걸린 실제 피해량. 배율이 아무리 낮아도 최소 1은 들어간다.
    private int EffectiveMeleeDamage =>
        ScaleDamage(meleeDamage, KindMultiplier(MoveType.Tackle) *
            (moves != null ? moves.TackleDamageMultiplier : 1f));
    private int EffectiveVineDamage =>
        ScaleDamage(vineDamage, KindMultiplier(MoveType.VineWhip));

    private static int ScaleDamage(int baseDamage, float multiplier) =>
        baseDamage <= 0 ? 0 : Mathf.Max(1, GameMath.RoundHalfUp(baseDamage * multiplier));

    /// <summary>기술에 붙은 속성(근접·원거리)에 걸린 유물·이벤트 배율.</summary>
    private static float KindMultiplier(MoveType move) =>
        AttackKinds.DamageMultiplier(MoveInfo.KindOf(move));

    private void Update()
    {
        // 공격 중 이동 감속 적용/해제
        controller.SpeedMultiplier = Time.time < slowUntil ? EffectiveAttackMoveSpeedMultiplier : 1f;

        if (!controller.ControlEnabled || (health != null && health.IsDead)) return;
        // 경직 중에는 공격도 못 한다. 후딜이 없는 것과 같아지면 경직을 넣은 의미가 없다.
        if (controller.IsStunned) return;
        // 강화 팔레트나 이벤트 대사창이 떠 있는 동안에는 클릭이 공격으로 새면 안 된다.
        if (MoveUpgradePanel.IsOpen || RelicChoicePanel.IsOpen || EventDialogue.IsOpen) return;

        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && CanUse(MoveType.Tackle))
        {
            lastMeleeTime = Time.time;
            MeleeAttack(GetMouseDirection());
        }
        else if (mouse != null && mouse.rightButton.wasPressedThisFrame && CanUse(MoveType.VineWhip))
        {
            lastVineTime = Time.time;
            VineWhipAttack(GetMouseDirection());
        }
        else if (kb != null && kb.leftShiftKey.wasPressedThisFrame && CanUse(MoveType.SeedSow))
        {
            seedUsedInRoom = CombatRoomController.VisitId;
            SowSeeds();
        }
        else if (kb != null && kb.spaceKey.wasPressedThisFrame && CanUse(MoveType.PetalDance))
        {
            lastPetalTime = Time.time;
            PetalDance();
        }
    }

    /// <summary>
    /// 기술은 전투방과 보스방에서만 쓸 수 있다. 상점·이벤트방에서는 때릴 대상도, 회복할 이유도 없다.
    /// </summary>
    public static bool MovesUsable => CombatRoomController.InCombatRoom;

    /// <summary>전투방이고, 배운 기술이고, 쿨타임도 끝났는지.</summary>
    private bool CanUse(MoveType move)
    {
        if (!MovesUsable) return false;
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
        int count = Physics2D.OverlapCircle(origin, EffectiveMeleeRadius, noFilter, hitBuffer);
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
        float width = EffectiveVineWidth;
        Vector2 origin = transform.position;
        Vector2 center = origin + direction * (range * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        StartCoroutine(VineWhipFlash(origin, direction, range, width));

        int damage = EffectiveVineDamage;
        struckTargets.Clear();
        int count = Physics2D.OverlapBox(center, new Vector2(range, width), angle,
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

    // ---------------------------------------------------------------- 기술 3·4 · 장판

    /// <summary>
    /// 씨뿌리기. 발밑에 초록 장판을 깔고, 그 위에 서 있는 동안 체력이 차오른다.
    /// 장판은 깔린 자리에 고정되므로 "서 있을 것인가 싸우러 나갈 것인가"가 곧 선택이 된다.
    /// </summary>
    private void SowSeeds()
    {
        MoveZone.SpawnHeal(transform.position, EffectiveSeedRadius, EffectiveSeedDuration,
                           EffectiveSeedHeal, seedTickInterval, seedColor);
    }

    /// <summary>
    /// 꽃잎댄스. 몸 주위에 분홍 장판이 돌고, 그 안에 있는 적을 주기적으로 때린다.
    /// 장판이 플레이어를 따라다니므로 적에게 붙어 있는 동안 계속 피해가 들어간다 —
    /// 근접 기술답게 "붙을 것인가"가 곧 선택이 된다.
    /// </summary>
    private void PetalDance()
    {
        MoveZone.SpawnDamage(transform.position, EffectivePetalRadius, EffectivePetalDuration,
                             EffectivePetalDamage, petalTickInterval, petalColor, transform);
    }

    /// <summary>채찍이 뻗었다가 사라지는 연출. 마커 하나를 계속 재사용한다.</summary>
    private IEnumerator VineWhipFlash(Vector2 origin, Vector2 direction, float range, float width)
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
            t.localScale = new Vector3(length, width, 1f);
            yield return null;
        }

        t.position = origin + direction * (range * 0.5f);
        t.localScale = new Vector3(range, width, 1f);
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
        flashMarker.transform.localScale = Vector3.one * EffectiveMeleeRadius * 2f;
        flashMarker.enabled = true;
        yield return flashDuration;
        if (flashMarker != null) flashMarker.enabled = false;
    }
}
