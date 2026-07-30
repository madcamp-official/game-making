using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어 기술의 실행부. 마우스 조준(360도 자유각) 기준이다.
///
/// 어느 캐릭터가 어느 기술을 어느 슬롯에 두는지는 <see cref="PlayerMoves"/>(캐릭터 데이터)가
/// 정하고, 여기는 <b>모든 캐릭터의 기술 구현</b>을 들고 있다가 슬롯이 가리키는 것을 실행한다.
///
/// * 이상해씨 계열 — 몸통박치기 / 덩굴채찍 / 씨뿌리기(회복 장판) / 꽃잎댄스(따라다니는 장판, 근접)
/// * 리자몽 계열 — 불꽃세례(투사체) / 용의춤(버프) / 드래곤클로(근접) / 화염방사(이어지는 줄기)
/// * 거북왕 계열 — 물대포(판정은 몸통박치기) / 파도타기(돌진) / 로켓박치기(무적 돌진) / 하이드로펌프(자리 고정 줄기)
///
/// 피해를 주는 기술에는 사거리 속성이 붙어 있다 (<see cref="MoveInfo.KindOf"/>).
/// 유물과 이벤트 강화는 그 속성만 보고 배율을 매긴다.
///
/// 기술은 전투방과 보스방에서, 적이 남아 있는 동안에만 쓸 수 있다 (<see cref="MovesUsable"/>).
/// 수치는 모두 Inspector에서 조정하고, 단계별 기준 위력은 캐릭터 데이터가 덮어쓴다
/// (<see cref="SetMovePowers"/>).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("기본 공격 1 — 근거리")]
    // 피해량은 진화 단계가 덮어쓴다(PlayerEvolution.stages). 여기 값은 1단계와 같게 맞춰 둔다.
    // 전 기술 일괄 10% 하향의 몫이 들어가 있다(12 → 11). 정수라 딱 10%로 떨어지지 않을 때는
    // 내림 쪽으로 붙였다 — 반올림으로 제자리에 남으면 하향이 아니게 된다.
    [SerializeField, Min(0)] private int meleeDamage = 11;
    [SerializeField, Min(0f)] private float meleeRange = 0.9f;
    [Tooltip("휘두르는 원의 반지름. 범위 강화 15%를 고르면 0.897이 되어, 예전 기본값 0.85보다 " +
             "약 5.5% 넓어진다.")]
    [SerializeField, Min(0f)] private float meleeRadius = 0.78f;
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
    [SerializeField, Min(0)] private int vineDamage = 4;
    [FormerlySerializedAs("razorCooldown")]
    [SerializeField, Min(0f)] private float vineCooldown = 2.2f;
    [Tooltip("채찍이 닿는 거리. 타일 한 칸이 1이다.")]
    [SerializeField, Min(0f)] private float vineRange = 2.8f;
    [Tooltip("채찍 판정의 굵기.")]
    [SerializeField, Min(0f)] private float vineWidth = 0.9f;
    [SerializeField, Min(0f)] private float vineKnockbackForce = 5f;
    [Tooltip("휘두른 뒤 움직이지 못하는 시간.")]
    [SerializeField, Min(0f)] private float vineStunDuration = 0.25f;
    [Tooltip("맞은 적에게 남는 속도의 비율. 0.55면 45% 느려진다. 1이면 감속이 없다. " +
             "걷는 속도만 깎으므로 이미 시작한 돌진은 그대로 간다.")]
    [SerializeField, Range(0.1f, 1f)] private float vineSlowMultiplier = 0.55f;
    [Tooltip("감속이 남는 시간. 강화 50%를 고르면 1.8초가 되어 이전 기본 성능을 되찾는다.")]
    [SerializeField, Min(0f)] private float vineSlowDuration = 1.2f;
    [SerializeField] private Color vineColor = new Color(0.3f, 0.85f, 0.25f, 0.95f);

    [Header("기술 3 — 씨뿌리기")]
    // 반지름 2에서 3.6으로 키웠다 (넓이 3.2배). 2층부터 쓰는 기술인데 그 난이도에서는
    // 지름 4칸짜리 장판 위에 가만히 서 있을 틈이 없어, 쓸 수 있는 길이 "한 마리만 남기고
    // 깔기" 하나로 굳었다. 그걸 떠올리지 못하면 이 기술은 <b>왜 있는지 알 수 없는 칸</b>이 된다.
    // 지름 7.2칸은 방(14×10)의 한쪽을 덮으므로, 싸우면서 그 안에 머무는 것이 가능해진다.
    [Tooltip("발밑에 까는 회복 장판의 반지름. 싸우면서 안에 머무를 수 있어야 하므로 넓다.")]
    [SerializeField, Min(0f)] private float seedRadius = 3.6f;
    [SerializeField, Min(0f)] private float seedDuration = 6f;
    [Tooltip("장판 위에 서 있는 동안 한 번에 차오르는 체력.")]
    [SerializeField, Min(0)] private int seedHealPerTick = 6;
    [SerializeField, Min(0.05f)] private float seedTickInterval = 1f;
    // 1층 숲 바닥이 초록이라 흔한 초록으로는 묻힌다. 밝은 연둣빛으로 띄운다.
    [SerializeField] private Color seedColor = new Color(0.6f, 1f, 0.45f, 0.4f);

    [Header("기술 4 — 꽃잎댄스 (원거리)")]
    // 반지름 1.8 → 1.6, 피해 배율 1.0 → 0.6.
    //
    // 3층에서 이 기술 하나가 방을 통째로 쓸었다. 배율 1.0이면 한 번 깔 때마다 <b>범위 안의
    // 모든 적에게</b> 몸통박치기 다섯 대(3단계 기준 110)가 들어가는데, 3층 잡몹의 체력이
    // 90~150이라 한 방에 여럿이 같이 죽었다. 게다가 장판은 따라다니므로 그동안 몸통박치기도
    // 그대로 친다 — 얹는 피해가 본체 피해를 넘어서면 다른 기술을 고를 이유가 사라진다.
    //
    // 최종 기술다운 무게는 남긴다: 배율 0.6이면 한 번에 65(3단계), 강화를 둘 다 걸면 90이다.
    // 여럿을 한꺼번에 <b>무르게 만드는</b> 기술이지 <b>정리하는</b> 기술은 아니게 된다.
    [SerializeField, Min(0f)] private float petalRadius = 1.6f;
    [SerializeField, Min(0f)] private float petalDuration = 2.5f;
    [Tooltip("한 틱의 피해량이 몸통박치기 기본 피해의 몇 배인지. 꽃잎댄스는 원거리 기술이지만 " +
             "기준값은 몸통박치기를 쓴다 — 덩굴채찍은 견제기라 피해가 낮아 기준으로 삼을 수 없다.")]
    [SerializeField, Min(0f)] private float petalDamageRatio = 0.6f;
    [SerializeField, Min(0.05f)] private float petalTickInterval = 0.5f;
    [SerializeField, Min(0f)] private float petalCooldown = 12f;
    [SerializeField] private Color petalColor = new Color(1f, 0.45f, 0.75f, 0.38f);

    // ---------------------------------------------------------------- 리자몽 계열

    [Header("불꽃세례 — 원거리 투사체")]
    // 피해량은 진화 단계가 덮어쓴다 (12/18/25). 여기 값은 1단계와 같게 맞춰 둔다.
    [SerializeField, Min(0)] private int fireSpitDamage = 12;
    [SerializeField, Min(0f)] private float fireSpitCooldown = 0.55f;
    [SerializeField, Min(0f)] private float fireSpitRange = 6.5f;
    [SerializeField, Min(0f)] private float fireSpitSpeed = 12f;
    [Tooltip("투사체의 지름(유닛). 판정과 그림이 같은 원이다.")]
    [SerializeField, Min(0f)] private float fireSpitSize = 0.4f;
    [SerializeField] private Color fireSpitColor = new Color(1f, 0.55f, 0.15f, 0.95f);

    [Header("용의춤 — 공격력·이동 속도 버프")]
    [Tooltip("공격력 증가 비율. 0.3이면 30% 세진다. 불꽃세례·드래곤클로·화염방사에만 적용되고 넉백은 그대로다.")]
    [SerializeField, Min(0f)] private float danceAttackBonus = 0.3f;
    [SerializeField, Min(0f)] private float danceSpeedBonus = 0.25f;
    [SerializeField, Min(0f)] private float danceDuration = 4f;
    [Tooltip("쿨타임은 기술을 쓴 순간부터 돈다 — 버프가 끝나기를 기다렸다 도는 게 아니다.")]
    [SerializeField, Min(0f)] private float danceCooldown = 12f;
    [SerializeField] private Color danceColor = new Color(1f, 0.35f, 0.3f, 0.8f);

    [Header("드래곤클로 — 근접 강타")]
    // 피해량은 진화 단계가 덮어쓴다 (26/38/52).
    [SerializeField, Min(0)] private int clawDamage = 26;
    [SerializeField, Min(0f)] private float clawCooldown = 4f;
    [SerializeField, Min(0f)] private float clawRange = 0.95f;
    [SerializeField, Min(0f)] private float clawRadius = 1.05f;
    [SerializeField, Min(0f)] private float clawKnockback = 9f;
    [SerializeField] private Color clawColor = new Color(1f, 0.5f, 0.2f, 0.55f);

    [Header("화염방사 — 이어지는 화염 줄기")]
    // 틱당 피해는 진화 단계가 덮어쓴다 (전 단계 14 — 총 6틱 84).
    [SerializeField, Min(0)] private int flameTickDamage = 14;
    [SerializeField, Min(0f)] private float flameDuration = 1.5f;
    [SerializeField, Min(0.05f)] private float flameTickInterval = 0.25f;
    [SerializeField, Min(0f)] private float flameCooldown = 9f;
    [SerializeField, Min(0f)] private float flameRange = 6f;
    [SerializeField, Min(0f)] private float flameWidth = 1.2f;
    [SerializeField] private Color flameColor = new Color(1f, 0.45f, 0.1f, 0.55f);

    // ---------------------------------------------------------------- 거북왕 계열

    [Header("물대포 — 근접 (판정은 몸통박치기와 동일)")]
    // 피해량은 진화 단계가 덮어쓴다 (10/15/21).
    [SerializeField, Min(0)] private int waterGunDamage = 10;
    [SerializeField, Min(0f)] private float waterGunCooldown = 0.5f;
    [SerializeField, Min(0f)] private float waterGunRange = 0.9f;
    [SerializeField, Min(0f)] private float waterGunRadius = 0.78f;
    [SerializeField, Min(0f)] private float waterGunKnockback = 6f;
    [SerializeField] private Color waterGunColor = new Color(0.35f, 0.7f, 1f, 0.55f);

    [Header("파도타기 — 이동 겸 공격 돌진")]
    // 피해량은 진화 단계가 덮어쓴다 (12/18/25).
    [SerializeField, Min(0)] private int surfDamage = 12;
    [SerializeField, Min(0f)] private float surfCooldown = 4.5f;
    [SerializeField, Min(0f)] private float surfDistance = 4.5f;
    [SerializeField, Min(0.05f)] private float surfDuration = 0.65f;
    [SerializeField, Min(0f)] private float surfKnockback = 5f;

    [Header("로켓박치기 — 짧은 무적 돌진")]
    // 피해량은 진화 단계가 덮어쓴다 (28/40/55).
    [SerializeField, Min(0)] private int rocketDamage = 28;
    [SerializeField, Min(0f)] private float rocketCooldown = 8f;
    [Tooltip("준비 동작. 이 동안에는 무적이 아니다 — 무적은 실제로 몸이 나가는 동안만이다.")]
    [SerializeField, Min(0f)] private float rocketWindup = 0.3f;
    [SerializeField, Min(0.05f)] private float rocketDuration = 0.6f;
    [SerializeField, Min(0f)] private float rocketDistance = 3.2f;
    [SerializeField, Min(0f)] private float rocketKnockback = 10f;

    [Header("하이드로펌프 — 자리를 고정하고 쏘는 물줄기")]
    // 틱당 피해는 진화 단계가 덮어쓴다 (전 단계 8 — 총 12틱 96).
    [SerializeField, Min(0)] private int hydroTickDamage = 8;
    [SerializeField, Min(0f)] private float hydroDuration = 2.4f;
    [SerializeField, Min(0.05f)] private float hydroTickInterval = 0.2f;
    [SerializeField, Min(0f)] private float hydroCooldown = 14f;
    [SerializeField, Min(0f)] private float hydroRange = 7f;
    [SerializeField, Min(0f)] private float hydroWidth = 1.1f;
    [Tooltip("조준이 마우스를 따라 도는 최대 각속도 (도/초).")]
    [SerializeField, Min(0f)] private float hydroTurnSpeed = 240f;
    [Tooltip("시전 중 받는 피해가 줄어드는 비율. 0.5면 절반만 맞는다. 완전 무적과는 다르다.")]
    [SerializeField, Range(0f, 0.9f)] private float hydroDamageReduction = 0.5f;
    [Tooltip("틱마다 맞은 적을 물줄기 방향으로 미는 힘.")]
    [SerializeField, Min(0f)] private float hydroPushForce = 3.5f;
    [SerializeField] private Color hydroColor = new Color(0.3f, 0.65f, 1f, 0.55f);

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
    private Rigidbody2D body;
    private PlayerDash dash;
    private PlayerCrowdControl crowdControl;
    private Camera mainCamera;
    private SpriteRenderer flashMarker; // 재사용하는 공격 판정 표시
    private SpriteRenderer vineMarker;  // 재사용하는 덩굴채찍 연출
    private SpriteRenderer beamMarker;  // 재사용하는 화염방사·하이드로펌프 줄기 연출
    private float lastMeleeTime = -999f;
    private float lastVineTime = -999f;
    private float lastPetalTime = -999f;
    private float lastFireSpitTime = -999f;
    private float lastDanceTime = -999f;
    private float lastClawTime = -999f;
    private float lastFlameTime = -999f;
    private float lastWaterGunTime = -999f;
    private float lastSurfTime = -999f;
    private float lastRocketTime = -999f;
    private float lastHydroTime = -999f;
    /// <summary>단계 데이터가 꽃잎댄스 위력을 따로 주지 않았을 때는 몸통박치기 위력을 쓴다.</summary>
    private int petalBaseDamage;
    /// <summary>씨뿌리기를 쓴 전투방의 번호. 방이 바뀌면 다시 쓸 수 있다.</summary>
    private int seedUsedInRoom = -1;
    private float slowUntil = -999f;
    /// <summary>용의춤이 끝나는 시각. 지나면 저절로 풀리므로 "되돌리기"가 없다 — 복구 실수도 없다.</summary>
    private float danceUntil = -999f;
    /// <summary>진행 중인 채널·돌진 루틴 (화염방사·로켓박치기·하이드로펌프). 하나만 돈다.</summary>
    private Coroutine busyRoutine;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        moves = GetComponent<PlayerMoves>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        crowdControl = GetComponent<PlayerCrowdControl>();
        // 돌진 부품은 캐릭터가 바뀌어도 같은 것을 쓰므로 없으면 여기서 붙인다.
        dash = GetComponent<PlayerDash>();
        if (dash == null) dash = gameObject.AddComponent<PlayerDash>();

        // 맞는 소리. OnDamaged가 아니라 OnCombatDamaged를 듣는 이유는, 잠만보를 흔들다 치르는
        // 대가처럼 "맞은 것이 아닌 감소"까지 타격음이 나면 안 되기 때문이다.
        // 맞아서 쓰러지는 순간에는 울리지 않는다 — 그 자리는 게임 오버 곡이 맡는다.
        if (health != null) health.OnCombatDamaged += GameAudio.PlayPlayerHurt;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnCombatDamaged -= GameAudio.PlayPlayerHurt;
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

    /// <summary>몸통박치기 판정 원의 반지름. 기술 강화와 광각렌즈가 함께 키운다.</summary>
    private float EffectiveMeleeRadius =>
        meleeRadius * (moves != null ? moves.TackleRadiusMultiplier : 1f) * RelicAttackSizeMultiplier;

    private float EffectiveAttackMoveSpeedMultiplier => attackMoveSpeedMultiplier;

    private float EffectiveVineCooldown =>
        vineCooldown * (moves != null ? moves.VineCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectiveVineRange =>
        vineRange * (moves != null ? moves.VineRangeMultiplier : 1f) * RelicAttackSizeMultiplier;
    /// <summary>채찍 판정의 굵기. 길이와 함께 광각렌즈를 탄다.</summary>
    private float EffectiveVineWidth => vineWidth * RelicAttackSizeMultiplier;
    private float EffectiveVineSlowDuration =>
        vineSlowDuration * (moves != null ? moves.VineSlowDurationMultiplier : 1f);

    // 씨뿌리기는 공격이 아니라 회복 장판이라 광각렌즈가 닿지 않는다. 지속시간만 빛의점토를 탄다.
    private float EffectiveSeedRadius =>
        seedRadius * (moves != null ? moves.SeedRadiusMultiplier : 1f);
    private float EffectiveSeedDuration =>
        seedDuration * (moves != null ? moves.SeedDurationMultiplier : 1f) * RelicZoneDurationMultiplier;
    private int EffectiveSeedHeal =>
        ScaleWholeValue(seedHealPerTick, moves != null ? moves.SeedHealMultiplier : 1f);

    private float EffectivePetalCooldown =>
        petalCooldown * (moves != null ? moves.PetalCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectivePetalRadius =>
        petalRadius * (moves != null ? moves.PetalRadiusMultiplier : 1f) * RelicAttackSizeMultiplier;
    private float EffectivePetalDuration => petalDuration * RelicZoneDurationMultiplier;

    /// <summary>
    /// 꽃잎댄스 한 틱의 피해. 몸통박치기의 <b>기본</b> 피해가 기준이라, 몸통박치기를 강화해도
    /// 이쪽이 같이 세지지는 않는다 — 한 기술의 강화가 다른 기술로 새면 선택의 뜻이 사라진다.
    /// 배율은 기술에 붙은 속성(근접)을 따른다.
    /// </summary>
    private int EffectivePetalDamage =>
        ScaleWholeValue(petalBaseDamage > 0 ? petalBaseDamage : meleeDamage,
            petalDamageRatio * KindMultiplier(MoveType.PetalDance) *
            (moves != null ? moves.PetalDamageMultiplier : 1f));

    // ---------------------------------------------------------------- 리자몽 계열 유효 수치

    /// <summary>
    /// 용의춤이 켜져 있는 동안의 공격력 배율. 불꽃세례·드래곤클로·화염방사에만 곱한다.
    /// 넉백에는 곱하지 않는다 — 명세가 못박은 규칙이다.
    /// 시각이 지나면 배율이 저절로 1로 돌아오므로 종료 처리에서 복구를 빠뜨릴 수가 없다.
    /// </summary>
    private float DanceAttackMultiplier =>
        Time.time < danceUntil ? 1f + danceAttackBonus + (moves != null ? moves.DancePowerBonus : 0f) : 1f;

    private float DanceSpeedMultiplier =>
        Time.time < danceUntil ? 1f + danceSpeedBonus + (moves != null ? moves.DanceSpeedBonus : 0f) : 1f;

    private float EffectiveDanceDuration =>
        danceDuration + (moves != null ? moves.DanceDurationBonus : 0f);
    private float EffectiveDanceCooldown => danceCooldown * RelicCooldownMultiplier;

    private int EffectiveFireSpitDamage =>
        ScaleWholeValue(fireSpitDamage, KindMultiplier(MoveType.FireSpit) * DanceAttackMultiplier *
            (moves != null ? moves.FireSpitDamageMultiplier : 1f));
    private float EffectiveFireSpitCooldown =>
        fireSpitCooldown * (moves != null ? moves.FireSpitCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectiveFireSpitSize =>
        fireSpitSize * (moves != null ? moves.FireSpitSizeMultiplier : 1f) * RelicAttackSizeMultiplier;

    private int EffectiveClawDamage =>
        ScaleWholeValue(clawDamage, KindMultiplier(MoveType.DragonClaw) * DanceAttackMultiplier *
            (moves != null ? moves.ClawDamageMultiplier : 1f));
    private float EffectiveClawCooldown => clawCooldown * RelicCooldownMultiplier;
    private float EffectiveClawRadius =>
        clawRadius * (moves != null ? moves.ClawRadiusMultiplier : 1f) * RelicAttackSizeMultiplier;
    private float EffectiveClawKnockback =>
        clawKnockback * (moves != null ? moves.ClawKnockbackMultiplier : 1f);

    private int EffectiveFlameTickDamage =>
        ScaleWholeValue(flameTickDamage, KindMultiplier(MoveType.Flamethrower) * DanceAttackMultiplier *
            (moves != null ? moves.FlameDamageMultiplier : 1f));
    private float EffectiveFlameCooldown =>
        flameCooldown * (moves != null ? moves.FlameCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectiveFlameWidth =>
        flameWidth * (moves != null ? moves.FlameWidthMultiplier : 1f) * RelicAttackSizeMultiplier;
    private float EffectiveFlameRange => flameRange * RelicAttackSizeMultiplier;

    // ---------------------------------------------------------------- 거북왕 계열 유효 수치

    private int EffectiveWaterGunDamage =>
        ScaleWholeValue(waterGunDamage, KindMultiplier(MoveType.WaterGun) *
            (moves != null ? moves.WaterGunDamageMultiplier : 1f));
    private float EffectiveWaterGunCooldown =>
        waterGunCooldown * (moves != null ? moves.WaterGunCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectiveWaterGunRadius =>
        waterGunRadius * (moves != null ? moves.WaterGunRadiusMultiplier : 1f) * RelicAttackSizeMultiplier;

    private int EffectiveSurfDamage =>
        ScaleWholeValue(surfDamage, KindMultiplier(MoveType.Surf) *
            (moves != null ? moves.SurfDamageMultiplier : 1f));
    private float EffectiveSurfCooldown =>
        surfCooldown * (moves != null ? moves.SurfCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectiveSurfDistance =>
        surfDistance * (moves != null ? moves.SurfDistanceMultiplier : 1f);

    private int EffectiveRocketDamage =>
        ScaleWholeValue(rocketDamage, KindMultiplier(MoveType.RocketHeadbutt) *
            (moves != null ? moves.RocketDamageMultiplier : 1f));
    private float EffectiveRocketCooldown =>
        rocketCooldown * (moves != null ? moves.RocketCooldownMultiplier : 1f) * RelicCooldownMultiplier;
    private float EffectiveRocketKnockback =>
        rocketKnockback * (moves != null ? moves.RocketKnockbackMultiplier : 1f);

    private int EffectiveHydroTickDamage =>
        ScaleWholeValue(hydroTickDamage, KindMultiplier(MoveType.HydroPump) *
            (moves != null ? moves.HydroDamageMultiplier : 1f));
    private float EffectiveHydroCooldown => hydroCooldown * RelicCooldownMultiplier;
    private float EffectiveHydroWidth =>
        hydroWidth * (moves != null ? moves.HydroWidthMultiplier : 1f) * RelicAttackSizeMultiplier;
    private float EffectiveHydroRange => hydroRange * RelicAttackSizeMultiplier;
    /// <summary>시전 중 받는 피해 배율. 감소 50%면 0.5, 강화하면 0.35까지 내려간다.</summary>
    private float EffectiveHydroDamageTaken =>
        Mathf.Clamp(1f - hydroDamageReduction - (moves != null ? moves.HydroGuardBonus : 0f), 0.05f, 1f);

    /// <summary>
    /// 씨뿌리기는 전투방마다 한 번만 쓸 수 있다. 시간이 지나 돌아오는 게 아니라
    /// 방을 넘어가야 돌아오므로, "이 방에서 언제 쓸 것인가"가 곧 선택이 된다.
    /// </summary>
    private bool SeedReady =>
        CombatRoomController.CombatActive && seedUsedInRoom != CombatRoomController.VisitId;

    /// <summary>
    /// 모든 기술의 쿨타임을 즉시 채운다. 방을 옮길 때 부른다.
    ///
    /// 방과 방 사이는 어차피 싸움이 없는 시간이라, 긴 쿨타임을 들고 통로에서 서성이는 것은
    /// 기다림일 뿐 선택이 아니었다. 방에 들어설 때 손패가 항상 갖춰져 있으면 "지금 쓸까,
    /// 아껴 둘까"를 <b>그 방 안에서</b> 정하게 된다.
    ///
    /// 씨뿌리기는 여기서 건드리지 않는다. 그쪽은 시간이 아니라 <b>방</b>으로 도는 쿨타임이라
    /// (<see cref="SeedReady"/>) 방이 바뀌면 저절로 돌아온다.
    /// </summary>
    public void ResetCooldowns()
    {
        // Time.time보다 확실히 앞선 값이면 어떤 쿨타임이든 이미 다 찬 것으로 계산된다.
        const float longAgo = -999f;
        lastMeleeTime = longAgo;
        lastVineTime = longAgo;
        lastPetalTime = longAgo;
        lastFireSpitTime = longAgo;
        lastDanceTime = longAgo;
        lastClawTime = longAgo;
        lastFlameTime = longAgo;
        lastWaterGunTime = longAgo;
        lastSurfTime = longAgo;
        lastRocketTime = longAgo;
        lastHydroTime = longAgo;
    }

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
            case MoveType.FireSpit: last = lastFireSpitTime; cooldown = EffectiveFireSpitCooldown; break;
            case MoveType.DragonDance: last = lastDanceTime; cooldown = EffectiveDanceCooldown; break;
            case MoveType.DragonClaw: last = lastClawTime; cooldown = EffectiveClawCooldown; break;
            case MoveType.Flamethrower: last = lastFlameTime; cooldown = EffectiveFlameCooldown; break;
            case MoveType.WaterGun: last = lastWaterGunTime; cooldown = EffectiveWaterGunCooldown; break;
            case MoveType.Surf: last = lastSurfTime; cooldown = EffectiveSurfCooldown; break;
            case MoveType.RocketHeadbutt: last = lastRocketTime; cooldown = EffectiveRocketCooldown; break;
            case MoveType.HydroPump: last = lastHydroTime; cooldown = EffectiveHydroCooldown; break;
            default: return 1f;
        }
        if (cooldown <= 0f) return 1f;
        return Mathf.Clamp01((Time.time - last) / cooldown);
    }

    /// <summary>
    /// 진화 단계의 슬롯별 위력을 현재 기술 구현에 나눠 준다. 배열이 없는 구버전 데이터는
    /// 기존 두 필드를 사용한다. 신규 기술은 실행부를 추가할 때 이 매핑에도 한 줄을 연결한다.
    /// </summary>
    public void SetMovePowers(int[] powers, int legacyPrimary, int legacySecondary)
    {
        meleeDamage = legacyPrimary;
        vineDamage = legacySecondary;
        petalBaseDamage = 0;

        if (moves == null || moves.MoveSet == null || powers == null) return;
        int count = Mathf.Min(moves.MoveCount, powers.Length);
        for (int slot = 0; slot < count; slot++)
        {
            int power = powers[slot];
            if (power <= 0) continue;

            switch (moves.MoveAt(slot))
            {
                case MoveType.Tackle: meleeDamage = power; break;
                case MoveType.VineWhip: vineDamage = power; break;
                case MoveType.PetalDance: petalBaseDamage = power; break;
                case MoveType.FireSpit: fireSpitDamage = power; break;
                case MoveType.DragonClaw: clawDamage = power; break;
                case MoveType.Flamethrower: flameTickDamage = power; break;
                case MoveType.WaterGun: waterGunDamage = power; break;
                case MoveType.Surf: surfDamage = power; break;
                case MoveType.RocketHeadbutt: rocketDamage = power; break;
                case MoveType.HydroPump: hydroTickDamage = power; break;
            }
        }
    }

    // 속성 배율이 걸린 실제 피해량. 배율이 아무리 낮아도 최소 1은 들어간다.
    private int EffectiveMeleeDamage =>
        ScaleWholeValue(meleeDamage, KindMultiplier(MoveType.Tackle) *
            (moves != null ? moves.TackleDamageMultiplier : 1f));
    private int EffectiveVineDamage =>
        ScaleWholeValue(vineDamage, KindMultiplier(MoveType.VineWhip));

    /// <summary>피해·회복처럼 정수로 적용되는 양수 능력치를 같은 반올림 규칙으로 배율 계산한다.</summary>
    private static int ScaleWholeValue(int baseValue, float multiplier) =>
        baseValue <= 0 ? 0 : Mathf.Max(1, GameMath.RoundHalfUp(baseValue * multiplier));

    /// <summary>기술에 붙은 속성(근접·원거리)에 걸린 유물·이벤트 배율.</summary>
    private static float KindMultiplier(MoveType move) =>
        AttackKinds.DamageMultiplier(MoveInfo.KindOf(move));

    private void Update()
    {
        // 공격 중 감속과 용의춤 가속. 겹치면 곱한다 — 춤을 춘 채 공격해도 가속의 몫은 남는다.
        float slow = Time.time < slowUntil ? EffectiveAttackMoveSpeedMultiplier : 1f;
        controller.SpeedMultiplier = slow * DanceSpeedMultiplier;

        if (!controller.ControlEnabled || (health != null && health.IsDead)) return;
        // 경직 중에는 공격도 못 한다. 후딜이 없는 것과 같아지면 경직을 넣은 의미가 없다.
        if (controller.IsStunned) return;
        // 채널(화염방사)·돌진이 도는 동안에는 다른 기술을 겹쳐 쓸 수 없다.
        if (busyRoutine != null || (dash != null && dash.IsDashing)) return;
        // 강화 팔레트·이벤트 대사창·보스 보상 화면이 떠 있는 동안에는 클릭이 공격으로 새면 안 된다.
        // 특히 보상 화면은 "아무 키나 눌러 계속"이라 넘기는 입력이 그대로 공격이 될 수 있다.
        if (MoveUpgradePanel.IsOpen || RelicChoicePanel.IsOpen || EventDialogue.IsOpen ||
            BossRewardSequence.IsRunning) return;
        // 일시정지 메뉴가 떠 있는 동안, 그리고 메뉴가 닫힌 그 프레임에도 마찬가지다 —
        // "계속하기"를 누른 클릭을 이 Update가 같은 프레임에 다시 보면 그대로 공격이 된다.
        if (GameFlow.Instance != null &&
            (GameFlow.Instance.Current != GameFlow.State.Playing ||
             GameFlow.Instance.MenuClosedFrame == Time.frameCount)) return;

        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame) TryUseSlot(0);
        else if (mouse != null && mouse.rightButton.wasPressedThisFrame) TryUseSlot(1);
        else if (kb != null && kb.leftShiftKey.wasPressedThisFrame) TryUseSlot(2);
        else if (kb != null && kb.spaceKey.wasPressedThisFrame) TryUseSlot(3);
    }

    /// <summary>
    /// 고정 입력을 현재 캐릭터 기술 세트의 슬롯으로 바꾼 뒤 해당 구현을 실행한다.
    /// 새 캐릭터를 추가할 때 입력과 HUD를 다시 만들 필요 없이, 신규 <see cref="MoveType"/>의
    /// 실행부만 이 분배기에 연결하면 된다.
    /// </summary>
    private void TryUseSlot(int slot)
    {
        if (moves == null || slot < 0 || slot >= moves.MoveCount) return;
        MoveType move = moves.MoveAt(slot);
        if (!CanUse(move)) return;

        switch (move)
        {
            case MoveType.Tackle:
                lastMeleeTime = Time.time;
                MeleeAttack(GetMouseDirection());
                break;
            case MoveType.VineWhip:
                lastVineTime = Time.time;
                VineWhipAttack(GetMouseDirection());
                break;
            case MoveType.SeedSow:
                seedUsedInRoom = CombatRoomController.VisitId;
                SowSeeds();
                break;
            case MoveType.PetalDance:
                lastPetalTime = Time.time;
                PetalDance();
                break;

            // 리자몽 계열
            case MoveType.FireSpit:
                lastFireSpitTime = Time.time;
                FireSpitAttack(GetMouseDirection());
                break;
            case MoveType.DragonDance:
                lastDanceTime = Time.time;
                DragonDanceBuff();
                break;
            case MoveType.DragonClaw:
                lastClawTime = Time.time;
                ClawAttack(GetMouseDirection());
                break;
            case MoveType.Flamethrower:
                lastFlameTime = Time.time;
                busyRoutine = StartCoroutine(FlameRoutine());
                break;

            // 거북왕 계열
            case MoveType.WaterGun:
                lastWaterGunTime = Time.time;
                WaterGunAttack(GetMouseDirection());
                break;
            case MoveType.Surf:
                lastSurfTime = Time.time;
                SurfDash(GetMouseDirection());
                break;
            case MoveType.RocketHeadbutt:
                lastRocketTime = Time.time;
                busyRoutine = StartCoroutine(RocketRoutine());
                break;
            case MoveType.HydroPump:
                lastHydroTime = Time.time;
                busyRoutine = StartCoroutine(HydroRoutine());
                break;

            default:
                Debug.LogWarning("기술 실행부가 연결되지 않았다: " + move);
                break;
        }
    }

    /// <summary>
    /// 기술은 전투방과 보스방에서, 그것도 <b>적이 남아 있는 동안에만</b> 쓸 수 있다.
    /// 상점·이벤트방에서는 때릴 대상도, 회복할 이유도 없다.
    ///
    /// 싸움이 끝난 뒤를 막는 이유는 씨뿌리기다. 마지막 적을 잡고 빈 방에서 장판을 깔면
    /// 방마다 한 번뿐인 기술이 위험 없는 회복이 되어, 언제 쓸지 고르는 재미가 사라진다.
    /// </summary>
    public static bool MovesUsable => CombatRoomController.CombatActive;

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

    /// <summary>
    /// 근접 공격용. 조준 방향을 보고, 공격 모션을 재생하며 그동안 감속한다.
    /// 동작 이름을 받는 이유: 캐릭터마다 공격 시트가 다르다 — 이상해씨는 Attack 하나로
    /// 다 쓰지만, 리자몽 계열은 기술마다 Shoot·Strike·Charge로 갈린다.
    /// </summary>
    private void BeginAttack(Vector2 direction, string animAction = "Attack")
    {
        controller.SetFacing(direction);
        slowUntil = Time.time + attackAnimDuration;
        if (playerAnimator != null)
            playerAnimator.PlayAction(animAction, attackAnimDuration);
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
            StartCoroutine(DebugAttackFlash(origin, EffectiveMeleeRadius,
                new Color(1f, 0.9f, 0.2f, 0.6f)));
        }

        StrikeCircle(direction, origin, EffectiveMeleeRadius, EffectiveMeleeDamage, meleeKnockbackForce);
    }

    /// <summary>
    /// 원형 근접 판정의 공용 실행부. 몸통박치기·물대포·드래곤클로가 수치만 달리해 함께 쓴다.
    /// 범위 안의 적을 전부 때리되, 콜라이더를 여럿 가진 적도 한 번만 맞는다.
    /// </summary>
    private void StrikeCircle(Vector2 direction, Vector2 origin, float radius, int damage, float knockback)
    {
        struckTargets.Clear();
        int count = Physics2D.OverlapCircle(origin, radius, noFilter, hitBuffer);
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
            enemy.ApplyKnockback(direction, knockback);
            PlayerRelicEffects.ReportDamageDealt(damage);
        }

        // 한 번 휘둘러 여럿을 때려도 소리는 한 번이다. 맞은 수만큼 겹쳐 울리면 무리 한가운데서
        // 휘두를 때마다 소리가 뭉개진다. 허공을 갈랐으면 아무 소리도 나지 않는다.
        if (struckTargets.Count > 0) GameAudio.PlayPlayerHit();
    }

    /// <summary>
    /// 덩굴채찍. 조준 방향으로 2칸 길이의 초록 채찍을 뻗어, 그 선 위에 닿은 적을 전부
    /// 때리고 <b>발을 묶는다.</b> 휘두른 뒤에는 짧게 경직이 걸려 바로 도망칠 수 없다 —
    /// 사거리를 준 대신 붙은 대가다.
    ///
    /// 피해는 몸통박치기의 4할이라 이 기술로 적을 잡을 수는 없다. 그래서 값어치를
    /// <b>거리를 벌리는 쪽</b>에 몰아 둔다 — 밀쳐 내고(넉백), 따라오는 발을 늦추고(감속),
    /// 그 사이에 자리를 다시 잡는다. 예전에는 밀쳐 내기만 해서 몸통박치기의 못한 판이었다.
    ///
    /// 기본 감속은 1.2초라 쿨타임 2.2초의 절반 남짓만 발을 묶는다. 감속 지속시간 강화를
    /// 고르면 50% 늘어난 1.8초가 되어 이전 기본 성능을 되찾는다. 쿨타임 강화까지 함께 골라도
    /// 1.8초 대 1.98초라 짧은 틈은 남고, 선제공격손톱까지 얻어야 사실상 계속 유지할 수 있다.
    /// </summary>
    private void VineWhipAttack(Vector2 direction)
    {
        // 근접 공격과 달리 공격 모션을 재생하지 않는다. 조준 방향만 바라보고,
        // 감속도 걸지 않는다 — 감속은 공격 모션 길이에 묶인 값인데 모션이 없고,
        // 어차피 경직 동안 못 움직인다.
        controller.SetFacing(direction);
        controller.Stun(vineStunDuration);

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
            if (vineSlowMultiplier < 1f) enemy.ApplySlow(vineSlowMultiplier, EffectiveVineSlowDuration);
            PlayerRelicEffects.ReportDamageDealt(damage);
        }

        if (struckTargets.Count > 0) GameAudio.PlayPlayerHit();
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

    // ---------------------------------------------------------------- 리자몽 계열

    /// <summary>
    /// 불꽃세례. 조준 방향으로 불덩이를 쏜다. 관통하지 않고, 적이나 벽에 닿으면 사라진다.
    /// 투사체는 프리팹 없이 코드로 세운다 — 원 하나짜리 그림이라 프리팹으로 만들 몫이 없다.
    /// </summary>
    private void FireSpitAttack(Vector2 direction)
    {
        BeginAttack(direction, "Shoot");

        float size = EffectiveFireSpitSize;
        GameObject go = new GameObject("FireSpit");
        go.transform.position = (Vector2)transform.position + direction * 0.4f;
        go.transform.localScale = Vector3.one * size;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveSprites.Circle;
        sr.color = fireSpitColor;
        sr.sortingOrder = 50;

        // Dynamic이어야 벽(정지 콜라이더)과의 트리거 접촉이 온다. 빠르니 연속 판정으로 굳힌다.
        Rigidbody2D projectileBody = go.AddComponent<Rigidbody2D>();
        projectileBody.gravityScale = 0f;
        projectileBody.freezeRotation = true;
        projectileBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.5f; // 원 스프라이트가 1유닛이라, 스케일이 곧 지름이 된다

        go.AddComponent<Projectile>().Launch(direction, EffectiveFireSpitDamage,
                                             fireSpitSpeed, fireSpitRange);
    }

    /// <summary>
    /// 용의춤. 잠깐 춤을 추고 공격력·이동 속도가 오른다. "되돌리기"가 없는 구조다 —
    /// 만료 시각만 적어 두고 배율 프로퍼티가 매번 시각을 보므로, 끝나는 순간 저절로 1로
    /// 돌아온다. 중복 시전도 시각을 늘릴 뿐이라 겹침 복구 문제가 없다.
    /// </summary>
    private void DragonDanceBuff()
    {
        danceUntil = Time.time + EffectiveDanceDuration;
        StartCoroutine(DancePulse());
    }

    /// <summary>몸에서 붉은 고리가 한 번 퍼진다. 버프가 걸렸다는 유일한 화면 신호다.</summary>
    private IEnumerator DancePulse()
    {
        GameObject go = new GameObject("DancePulse");
        SpriteRenderer ring = go.AddComponent<SpriteRenderer>();
        ring.sprite = PrimitiveSprites.Ring;
        ring.sortingOrder = 50;

        const float PulseTime = 0.45f;
        float elapsed = 0f;
        while (elapsed < PulseTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / PulseTime;
            go.transform.position = transform.position; // 달리면서 춰도 몸을 따라온다
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.6f, t);
            ring.color = new Color(danceColor.r, danceColor.g, danceColor.b, danceColor.a * (1f - t));
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>드래곤클로. 몸통박치기와 같은 원형 판정에 수치만 무겁다 — 느리고 넓고 세다.</summary>
    private void ClawAttack(Vector2 direction)
    {
        BeginAttack(direction, "Strike");
        Vector2 origin = (Vector2)transform.position + direction * clawRange;
        StartCoroutine(DebugAttackFlash(origin, EffectiveClawRadius, clawColor));
        StrikeCircle(direction, origin, EffectiveClawRadius, EffectiveClawDamage, EffectiveClawKnockback);
    }

    /// <summary>
    /// 화염방사. 1.5초 동안 마우스를 따라 도는 화염 줄기를 뿜는다. 걸을 수는 있지만
    /// 공격 감속(50%)이 시전 내내 걸린다 — <see cref="slowUntil"/>을 끝 시각까지 미는 것이 전부다.
    ///
    /// 틱은 장판(<see cref="MoveZone"/>)과 같은 이유로 <see cref="Health.TakeToll"/>을 쓴다:
    /// 0.25초 간격이 적의 피격 무적(0.3초)에 걸리면 틱이 통째로 사라진다.
    /// </summary>
    private IEnumerator FlameRoutine()
    {
        float endTime = Time.time + flameDuration;
        float nextTick = Time.time;
        slowUntil = endTime;
        if (playerAnimator != null) playerAnimator.BeginChannel("Charge", flameDuration);

        try
        {
            while (Time.time < endTime)
            {
                if ((health != null && health.IsDead) || !MovesUsable) yield break;

                Vector2 direction = GetMouseDirection();
                controller.SetFacing(direction);
                ShowBeam(direction, EffectiveFlameRange, EffectiveFlameWidth, flameColor);

                if (Time.time >= nextTick)
                {
                    nextTick += flameTickInterval;
                    BeamTick(direction, EffectiveFlameRange, EffectiveFlameWidth,
                             EffectiveFlameTickDamage, pushForce: 0f);
                }
                yield return null;
            }
        }
        finally
        {
            // 어느 길로 끝나든 줄기·모션·감속을 걷는다. 중단이 남긴 상태가 없어야 한다.
            HideBeam();
            if (playerAnimator != null) playerAnimator.EndChannel();
            slowUntil = Mathf.Min(slowUntil, Time.time);
            busyRoutine = null;
        }
    }

    // ---------------------------------------------------------------- 거북왕 계열

    /// <summary>물대포. 이름만 원거리 같을 뿐, 판정은 몸통박치기와 같은 근접 원이다.</summary>
    private void WaterGunAttack(Vector2 direction)
    {
        BeginAttack(direction, "Shoot");
        Vector2 origin = (Vector2)transform.position + direction * waterGunRange;
        StartCoroutine(DebugAttackFlash(origin, EffectiveWaterGunRadius, waterGunColor));
        StrikeCircle(direction, origin, EffectiveWaterGunRadius, EffectiveWaterGunDamage, waterGunKnockback);
    }

    /// <summary>
    /// 파도타기. 시전 순간의 마우스 방향으로 고정된 돌진. 실제 이동·타격·벽 판정은
    /// <see cref="PlayerDash"/>가 맡고, 여기서는 경직(돌진 중 입력 차단)과 모션만 건다.
    /// </summary>
    private void SurfDash(Vector2 direction)
    {
        controller.SetFacing(direction);
        controller.Stun(surfDuration);
        if (playerAnimator != null) playerAnimator.PlayAction("Walk", surfDuration);
        float speed = EffectiveSurfDistance / surfDuration;
        dash.Begin(direction, speed, surfDuration, EffectiveSurfDamage, surfKnockback,
                   grantInvulnerability: false);
        StartCoroutine(ReleaseStunWhenDashEnds());
    }

    /// <summary>
    /// 로켓박치기. 준비 동작(무적 아님) 뒤 짧고 굵게 무적 돌진한다.
    /// 방향은 시전 순간에 고정된다 — 준비 동작은 조준 시간이 아니라 예고 시간이다.
    /// 무적의 시작·해제는 전부 <see cref="PlayerDash"/> 안에 있다.
    /// </summary>
    private IEnumerator RocketRoutine()
    {
        Vector2 direction = GetMouseDirection();
        controller.SetFacing(direction);
        controller.Stun(rocketWindup + rocketDuration);
        if (playerAnimator != null)
            playerAnimator.PlayAction("Ricochet", rocketWindup + rocketDuration);

        try
        {
            yield return new WaitForSeconds(rocketWindup);
            if ((health != null && health.IsDead) || !MovesUsable) yield break;

            float speed = rocketDistance / rocketDuration;
            dash.Begin(direction, speed, rocketDuration, EffectiveRocketDamage,
                       EffectiveRocketKnockback, grantInvulnerability: true);
            while (dash.IsDashing) yield return null;
            ReleaseOwnStun();
        }
        finally
        {
            busyRoutine = null;
        }
    }

    /// <summary>돌진이 벽에서 일찍 끝나면 남겨 둔 경직도 함께 푼다.</summary>
    private IEnumerator ReleaseStunWhenDashEnds()
    {
        while (dash != null && dash.IsDashing) yield return null;
        ReleaseOwnStun();
    }

    /// <summary>
    /// 기술이 미리 걸어 둔 경직을 지운다. 빙결(적 CC)이 건 경직까지 지우면 안 되므로
    /// 얼어 있는 동안에는 그대로 둔다.
    /// </summary>
    private void ReleaseOwnStun()
    {
        if (crowdControl == null) crowdControl = GetComponent<PlayerCrowdControl>();
        if (crowdControl != null && crowdControl.IsFrozen) return;
        controller.CancelStun();
    }

    /// <summary>
    /// 하이드로펌프. 2.4초 동안 자리를 고정하고 마우스를 따라 도는 물줄기를 쏜다.
    ///
    /// * 자리 고정 — Rigidbody 위치를 통째로 얼린다. 속도로 밀어내는 CC(해류·흡인)까지
    ///   물리 단계에서 막힌다. 경직도 함께 걸어 입력과 다른 기술을 잠근다.
    /// * 받는 피해 50% 감소 — <see cref="Health.DamageTakenMultiplier"/>. 완전 무적
    ///   (<see cref="Health.BeginInvulnerability"/>)과는 다른 층이다.
    /// * 조준 — 목표각으로 초당 240도까지만 돈다. 마우스를 홱 돌려도 줄기는 따라 도는
    ///   중간 각도를 전부 지난다.
    ///
    /// 고정·감속·경직은 전부 finally에서 되돌린다 — 사망·방 종료로 끊겨도 남지 않는다.
    /// </summary>
    private IEnumerator HydroRoutine()
    {
        Vector2 aim = GetMouseDirection();
        float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        float endTime = Time.time + hydroDuration;
        float nextTick = Time.time;

        controller.SetFacing(aim);
        controller.Stun(hydroDuration);
        if (playerAnimator != null) playerAnimator.BeginChannel("Charge", hydroDuration);

        RigidbodyConstraints2D previousConstraints = body.constraints;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        float previousDamageTaken = health != null ? health.DamageTakenMultiplier : 1f;
        if (health != null) health.DamageTakenMultiplier = EffectiveHydroDamageTaken;

        try
        {
            while (Time.time < endTime)
            {
                if ((health != null && health.IsDead) || !MovesUsable) yield break;

                // 목표각을 향해 제한 속도로 돈다.
                Vector2 mouseDir = GetMouseDirection();
                float target = Mathf.Atan2(mouseDir.y, mouseDir.x) * Mathf.Rad2Deg;
                aimAngle = Mathf.MoveTowardsAngle(aimAngle, target, hydroTurnSpeed * Time.deltaTime);
                Vector2 direction = new Vector2(Mathf.Cos(aimAngle * Mathf.Deg2Rad),
                                                Mathf.Sin(aimAngle * Mathf.Deg2Rad));
                controller.SetFacing(direction);
                ShowBeam(direction, EffectiveHydroRange, EffectiveHydroWidth, hydroColor);

                if (Time.time >= nextTick)
                {
                    nextTick += hydroTickInterval;
                    BeamTick(direction, EffectiveHydroRange, EffectiveHydroWidth,
                             EffectiveHydroTickDamage, hydroPushForce);
                }
                yield return null;
            }
        }
        finally
        {
            body.constraints = previousConstraints;
            if (health != null) health.DamageTakenMultiplier = previousDamageTaken;
            HideBeam();
            if (playerAnimator != null) playerAnimator.EndChannel();
            ReleaseOwnStun();
            busyRoutine = null;
        }
    }

    // ---------------------------------------------------------------- 줄기(빔) 공용부

    /// <summary>줄기 한 틱. 플레이어 앞으로 뻗은 직사각형 안의 적을 전부 때린다.</summary>
    private void BeamTick(Vector2 direction, float range, float width, int damage, float pushForce)
    {
        Vector2 origin = transform.position;
        Vector2 center = origin + direction * (range * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        struckTargets.Clear();
        int count = Physics2D.OverlapBox(center, new Vector2(range, width), angle, noFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = hitBuffer[i].GetComponentInParent<EnemyController>();
            if (enemy == null) continue;
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (struckTargets.Contains(enemyHealth)) continue;
            struckTargets.Add(enemyHealth);

            // 장판과 같은 이유로 무적 시간을 쓰지 않는다 — 틱 간격이 피격 무적보다 짧다.
            enemyHealth.TakeToll(damage);
            if (pushForce > 0f) enemy.ApplyKnockback(direction, pushForce);
            PlayerRelicEffects.ReportDamageDealt(damage);
        }
    }

    /// <summary>줄기 연출. 마커 하나를 재사용하며 매 프레임 조준에 맞춰 눕힌다.</summary>
    private void ShowBeam(Vector2 direction, float range, float width, Color color)
    {
        if (beamMarker == null)
        {
            EnsureWhiteSprite();
            GameObject marker = new GameObject("MoveBeam");
            beamMarker = marker.AddComponent<SpriteRenderer>();
            beamMarker.sprite = whiteSprite;
            beamMarker.sortingOrder = 50;
        }

        Transform t = beamMarker.transform;
        t.position = (Vector2)transform.position + direction * (range * 0.5f);
        t.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        t.localScale = new Vector3(range, width, 1f);
        beamMarker.color = color;
        beamMarker.enabled = true;
    }

    private void HideBeam()
    {
        if (beamMarker != null) beamMarker.enabled = false;
    }

    // ---------------------------------------------------------------- 새 판 초기화

    /// <summary>
    /// 새 판(재도전 포함)을 시작한다. 쿨타임·버프·채널·돌진을 전부 걷는다.
    /// 씬을 다시 올리지 않고 이어서 도는 구조라, 지난 판의 쿨타임과 용의춤이 그대로
    /// 넘어오면 판의 시작이 캐릭터마다 달라진다. <see cref="GameFlow"/>가 부른다.
    ///
    /// 채널을 멈추면(StopCoroutine) 이터레이터가 정리되며 finally가 돌아, 위치 고정과
    /// 피해 감소도 이 한 줄로 원상 복구된다.
    /// </summary>
    public void ResetForNewRun()
    {
        if (busyRoutine != null) { StopCoroutine(busyRoutine); busyRoutine = null; }
        if (dash != null) dash.End();
        HideBeam();
        if (playerAnimator != null) playerAnimator.EndChannel();

        lastMeleeTime = lastVineTime = lastPetalTime = -999f;
        lastFireSpitTime = lastDanceTime = lastClawTime = lastFlameTime = -999f;
        lastWaterGunTime = lastSurfTime = lastRocketTime = lastHydroTime = -999f;
        danceUntil = -999f;
        slowUntil = -999f;
        seedUsedInRoom = -1;
        if (controller != null) controller.CancelStun();
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
    // 마커 오브젝트는 한 번만 만들어 재사용한다. 색은 기술마다 다르다 —
    // 몸통박치기는 노랑, 드래곤클로는 주황, 물대포는 물빛.
    private IEnumerator DebugAttackFlash(Vector2 origin, float radius, Color color)
    {
        if (flashMarker == null)
        {
            EnsureWhiteSprite();
            GameObject marker = new GameObject("AttackFlash");
            flashMarker = marker.AddComponent<SpriteRenderer>();
            flashMarker.sprite = whiteSprite;
            flashMarker.sortingOrder = 50;
        }

        flashMarker.color = color;
        flashMarker.transform.position = origin;
        flashMarker.transform.localScale = Vector3.one * radius * 2f;
        flashMarker.enabled = true;
        yield return flashDuration;
        if (flashMarker != null) flashMarker.enabled = false;
    }
}
