using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 3층 최종 보스 갸라도스의 전투 로직.
///
/// 버터플과 코뿌리는 "무엇을 피할 것인가"를 묻는 보스였다. 갸라도스는 거기에 <b>언제 때릴 수
/// 있는가</b>를 더한다. 전투장 안팎을 오가며 때릴 수 있는 시간(<c>Exposed</c>)과 살아남기만 하는
/// 시간(<c>Submerged</c>)이 교대로 오고, 그 위에 전투 내내 도는 삼중 해류가 겹친다.
///
/// * 잠항 — 반사 하이드로펌프, 격류 압착. 보스는 무적이고 바깥 바다에서 공격만 보낸다.
/// * 노출 — 잉어킹 소환, 똬리치기. 이 시간에만 갸라도스를 때릴 수 있다.
///
/// 노출은 <b>고정된 시간</b>으로 끝나고 잠항은 <b>사용한 패턴 수</b>로 끝난다. 그래서 센 빌드는
/// 같은 시간에 더 많은 피해를 넣을 수 있고, 약한 빌드라고 노출 시간이 늘어나지도 않는다.
///
/// 체력 절반에서 2페이즈로 넘어가되 새 패턴은 늘리지 않는다. 같은 규칙이 더 빠르고 넓어질 뿐이다.
///
/// 이동·무적·패턴·페이즈를 모두 이 컴포넌트가 맡으므로 <see cref="EnemyController"/>의 기본
/// 추적 AI는 꺼 둔다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody2D))]
public class GyaradosBossController : MonoBehaviour
{
    private enum State { Intro, Exposed, Exit, Submerged, Enter }
    private enum OuterPattern { HydroPump, Crush }
    private enum InnerPattern { Summon, Coil }
    private enum FloodSide { Left, Right, Bottom, Top }

    // ---------------------------------------------------------------- 패턴 설정

    [System.Serializable]
    private class CurrentSettings
    {
        [Tooltip("플레이어의 X 이동에 더해지는 속도. 난도를 올릴 때 여기부터 손대지 않는다.")]
        [Min(0f)] public float speed = 1.05f;
        [Tooltip("한 방향을 유지하는 최소 시간")]
        [Min(0.1f)] public float minHold = 5f;
        [Tooltip("한 방향을 유지하는 최대 시간")]
        [Min(0.1f)] public float maxHold = 7f;
        [Tooltip("방향이 바뀌기 전 화살표가 번갈아 깜빡이는 시간")]
        [Min(0.05f)] public float telegraph = 0.85f;
    }

    [System.Serializable]
    private class HydroSettings
    {
        [Tooltip("전투장 벽에서 튕기는 횟수. 1페이즈는 1, 2페이즈는 2다. 진입은 반사로 세지 않는다.")]
        [FormerlySerializedAs("refractions")]
        [Range(1, 2)] public int reflections = 1;
        [Tooltip("최초 발사 방향만 보여 주는 예고 시간. 발사는 즉발이라 이 시간이 유일한 회피 여유다.")]
        [Min(0.05f)] public float telegraph = 0.65f;
        [Min(0.05f)] public float width = 0.8f;
        [Tooltip("번쩍인 경로가 남아 피해 판정을 유지하는 시간")]
        [Min(0f)] public float trailDuration = 0.5f;
        [Min(0)] public int damage = 28;
        [Min(0f)] public float recovery = 0.45f;
        [Tooltip("반사점이 모서리에서 떨어져야 하는 최소 거리. 모서리 이중 반사를 막는다.")]
        [Min(0f)] public float cornerMargin = 0.6f;
        [Tooltip("이어지는 두 접점 사이의 최소 길이. 벽에 붙어 떠는 짧은 구간을 막는다.")]
        [Min(0f)] public float minSegmentLength = 1f;
    }

    [System.Serializable]
    private class FloodSettings
    {
        [Tooltip("각 축 길이의 몇 할까지 물이 차오르는지")]
        [Range(0.1f, 0.9f)] public float depthRatio = 0.55f;
        [Tooltip("첫째·둘째 범람의 예고 시간")]
        [Min(0.05f)] public float telegraph = 1.05f;
        [Tooltip("두 번째 범람이 활성화된 뒤 유지하는 시간")]
        [Min(0f)] public float holdAfterSecond = 1f;
        [Tooltip("2페이즈 세 번째 범람의 예고 시간. 먼 거리를 요구하므로 더 길다.")]
        [Min(0.05f)] public float thirdTelegraph = 1.3f;
        [Tooltip("2페이즈 마지막 조합을 유지하는 시간")]
        [Min(0f)] public float holdAfterThird = 0.85f;
        [Min(0)] public int damage = 28;
        [Tooltip("물 안에 계속 서 있을 때 피해를 다시 시도하는 간격")]
        [Min(0.05f)] public float damageRetryInterval = 0.6f;
        [Tooltip("맞았을 때 이동 속도가 몇 할로 줄어드는지. 0.75면 25% 감속이다.")]
        [Range(0.1f, 1f)] public float slowMultiplier = 0.75f;
        [Min(0f)] public float slowDuration = 0.75f;
        [Min(0f)] public float recovery = 0.45f;
    }

    [System.Serializable]
    private class SummonSettings
    {
        [Min(1)] public int count = 3;
        [Tooltip("원형 물결이 뜨고 잉어킹이 튀어 오르기까지의 시간")]
        [Min(0.05f)] public float telegraph = 0.75f;
        [Min(1)] public int magikarpHealth = 70;
        [Min(0.05f)] public float bodyRadius = 0.55f;
        [Min(0f)] public float recovery = 0.45f;
    }

    [System.Serializable]
    private class CoilSettings
    {
        [Tooltip("바깥 고리의 안쪽 반지름. 이 안쪽은 안전하다. 키울수록 고리의 위험 범위가 좁아진다.")]
        [Min(0f)] public float ringInner = 1.85f;
        [Min(0f)] public float ringOuter = 4.5f;
        [Tooltip("안쪽 원의 반지름. 중심부터 여기까지가 위험하다.")]
        [Min(0f)] public float innerRadius = 1.85f;
        [Min(0.05f)] public float firstTelegraph = 0.65f;
        [Min(0.05f)] public float secondTelegraph = 0.55f;
        [Tooltip("첫 타격 뒤 두 번째 예고를 시작하기까지의 간격")]
        [Min(0f)] public float betweenStrikes = 0.55f;
        [Min(0)] public int ringDamage = 30;
        [Min(0)] public int innerDamage = 32;
        [Min(0f)] public float recovery = 0.55f;
    }

    /// <summary>
    /// 플레이어 피격 무적(0.5초)보다 반드시 길어야 하는 두 타격 사이 간격.
    /// Inspector 값이 더 짧아도 여기서 붙들어, 두 번째 판정이 첫 피격의 무적에 통째로 먹히지 않게 한다.
    /// </summary>
    private const float MinStrikeInterval = 0.55f;
    /// <summary>물줄기가 벽에 붙어 떨지 않도록 반사점에서 새 방향으로 더하는 여유값.</summary>
    private const float ReflectionEpsilon = 0.03f;
    /// <summary>
    /// 조준 후보가 모두 실패했을 때 쓰는 예비 각도. 발사 면에서 전투장 중심으로 향하는 정면을
    /// 좌우로 기울인 값이며, 정면(0도)은 반대쪽 벽에 수직으로 부딪혀 되짚어 오므로 넣지 않는다.
    /// </summary>
    private static readonly float[] HydroFallbackTilts = { 22f, -22f, 38f, -38f, 55f, -55f };
    /// <summary>공중을 지나가는 연출은 캐릭터(10)보다 앞에 그린다.</summary>
    private const int AirborneSortingOrder = 12;

    // ---------------------------------------------------------------- Inspector

    [Header("맵 기준점 — F3Room7_Boss에 배치해 연결한다")]
    [Tooltip("중앙 플레이 영역. 위치가 중심, localScale이 전체 크기다. 비워 두면 부모(방)와 아래 반크기를 쓴다.")]
    [SerializeField] private Transform arenaBounds;
    [Tooltip("전투 계산에 쓰는 반너비·반높이. arenaBounds가 있으면 그쪽이 우선한다.")]
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(6.2f, 4.2f);
    [Tooltip("하이드로펌프가 반사되는 네 벽의 안쪽 면. 물대포가 부딪히는 벽과 눈으로 일치해야 한다. " +
             "비워 두면 arenaBounds를 쓰지만, 그 면이 벽과 다르면 허공에서 꺾여 보인다.")]
    [SerializeField] private Transform hydroReflectBounds;
    [Tooltip("노출 상태에서 갸라도스가 머무는 자리. 비워 두면 전투장 중앙.")]
    [SerializeField] private Transform exposedAnchor;
    [Tooltip("외곽 바다의 잠항 위치. 비워 둔 자리는 전투장 밖으로 자동 계산한다.")]
    [SerializeField] private Transform diveAnchorTop;
    [SerializeField] private Transform diveAnchorBottom;
    [SerializeField] private Transform diveAnchorLeft;
    [SerializeField] private Transform diveAnchorRight;
    [Tooltip("하이드로펌프가 중앙으로 들어오는 네 방향 발사 원점. 비워 둔 자리는 반사 경계 밖으로 자동 계산한다.")]
    [SerializeField] private Transform hydroOriginTop;
    [SerializeField] private Transform hydroOriginBottom;
    [SerializeField] private Transform hydroOriginLeft;
    [SerializeField] private Transform hydroOriginRight;
    [Tooltip("세 수로의 표시 기준점. 판정 경계는 arenaHalfSize.y에서 계산하며, 이 기준점은 배치 확인용이다.")]
    [SerializeField] private Transform currentLaneTop;
    [SerializeField] private Transform currentLaneMiddle;
    [SerializeField] private Transform currentLaneBottom;

    [Header("소환물")]
    [Tooltip("전투 전용 잉어킹 프리팹. 이벤트방 오브젝트를 그대로 쓰지 않는다.")]
    [SerializeField] private MagikarpObstacle magikarpPrefab;

    [Header("상태 시간")]
    [Tooltip("포효와 물결을 보여 주는 시간. 이 동안 보스만 무적이고 둘 다 움직일 수 있다.")]
    [SerializeField, Min(0f)] private float introDuration = 0.8f;
    [SerializeField, Min(0f)] private float exitDuration = 0.34f;
    [SerializeField, Min(0f)] private float enterDuration = 0.42f;
    [Tooltip("진입 위치가 플레이어와 이만큼 가까우면 옆으로 비켜 올라온다.")]
    [SerializeField, Min(0f)] private float enterClearance = 1.6f;

    [Header("노출 상태")]
    [SerializeField, Min(1f)] private float exposedDurationPhase1 = 11f;
    [SerializeField, Min(1f)] private float exposedDurationPhase2 = 9f;
    [SerializeField, Min(0f)] private float innerGapPhase1 = 0.22f;
    [SerializeField, Min(0f)] private float innerGapPhase2 = 0.14f;
    [Tooltip("내부 패턴 셔플 백 한 벌에 넣는 똬리치기 수. 소환보다 화면 시간이 두 배 넘게 길어 " +
             "반씩 넣으면 똬리치기만 하는 것처럼 느껴진다.")]
    [SerializeField, Min(1)] private int coilPerInnerBag = 1;
    [Tooltip("내부 패턴 셔플 백 한 벌에 넣는 잉어킹 소환 수.")]
    [SerializeField, Min(1)] private int summonPerInnerBag = 2;

    [Header("잠항 상태")]
    [Tooltip("한 번의 잠항에서 사용하는 외부 패턴 수")]
    [SerializeField, Range(1, 2)] private int outerPatternsPhase1 = 1;
    [SerializeField, Range(1, 2)] private int outerPatternsPhase2 = 2;
    [SerializeField, Min(0f)] private float outerGapPhase2 = 0.18f;

    [Header("삼중 해류")]
    [SerializeField] private CurrentSettings currentPhase1 = new CurrentSettings
    {
        speed = 1.45f, minHold = 2.8f, maxHold = 4f, telegraph = 0.6f,
    };
    [SerializeField] private CurrentSettings currentPhase2 = new CurrentSettings
    {
        speed = 1.6f, minHold = 2f, maxHold = 3f, telegraph = 0.5f,
    };
    [Tooltip("수로 하나에 그리는 화살표 수")]
    [SerializeField, Min(2)] private int arrowsPerLane = 7;
    [Tooltip("화살표가 흘러가는 속도. 연출일 뿐 판정과는 무관하다.")]
    [SerializeField, Min(0f)] private float arrowScrollSpeed = 1.4f;

    [Header("하이드로펌프")]
    [SerializeField] private HydroSettings hydroPhase1 = new HydroSettings
    {
        reflections = 1, telegraph = 0.58f,
        width = 0.8f, trailDuration = 0.35f, damage = 28, recovery = 0.2f,
        cornerMargin = 0.6f, minSegmentLength = 1f,
    };
    [SerializeField] private HydroSettings hydroPhase2 = new HydroSettings
    {
        reflections = 2, telegraph = 0.5f,
        width = 0.9f, trailDuration = 0.4f, damage = 32, recovery = 0.15f,
        cornerMargin = 0.6f, minSegmentLength = 1f,
    };
    [Tooltip("경로 후보를 몇 번까지 다시 만들지. 실패하면 조준점을 전투장 중심 쪽으로 당겨 가며 다시 시도한다.")]
    [SerializeField, Min(1)] private int hydroAimAttempts = 6;

    [Header("격류 압착")]
    [SerializeField] private FloodSettings floodPhase1 = new FloodSettings
    {
        depthRatio = 0.4f, telegraph = 0.68f, holdAfterSecond = 0.6f,
        damage = 28, damageRetryInterval = 0.6f, slowMultiplier = 0.75f, slowDuration = 0.75f,
        recovery = 0.2f,
    };
    [SerializeField] private FloodSettings floodPhase2 = new FloodSettings
    {
        depthRatio = 0.4f, telegraph = 0.6f, holdAfterSecond = 0.5f,
        thirdTelegraph = 0.85f, holdAfterThird = 0.5f,
        damage = 32, damageRetryInterval = 0.55f, slowMultiplier = 0.7f, slowDuration = 0.8f,
        recovery = 0.14f,
    };

    [Header("잉어킹 소환")]
    [SerializeField] private SummonSettings summonPhase1 = new SummonSettings
    {
        count = 3, telegraph = 0.52f, magikarpHealth = 70, bodyRadius = 0.55f, recovery = 0.2f,
    };
    [SerializeField] private SummonSettings summonPhase2 = new SummonSettings
    {
        count = 4, telegraph = 0.45f, magikarpHealth = 90, bodyRadius = 0.55f, recovery = 0.14f,
    };

    [Header("잉어킹 배치 규칙")]
    [SerializeField, Min(0f)] private float minDistanceFromPlayer = 1.25f;
    [SerializeField, Min(0f)] private float minDistanceFromBoss = 1.8f;
    [SerializeField, Min(0f)] private float minDistanceFromWall = 0.85f;
    [SerializeField, Min(0f)] private float minDistanceBetweenMagikarp = 1.35f;
    [Tooltip("플레이어가 지나갈 수 있어야 하는 최소 폭. 잉어킹과 벽 사이, 수로 경계의 틈에 모두 쓴다.")]
    [SerializeField, Min(0f)] private float safeCorridorWidth = 1.25f;
    [Tooltip("갸라도스 주위가 완전히 막히지 않도록 요구하는 최소 각도(도).")]
    [SerializeField, Range(10f, 180f)] private float minEscapeAngle = 70f;

    [Header("똬리치기")]
    [SerializeField] private CoilSettings coilPhase1 = new CoilSettings
    {
        ringInner = 1.85f, ringOuter = 4.5f, innerRadius = 1.85f,
        firstTelegraph = 0.48f, secondTelegraph = 0.4f, betweenStrikes = 0.55f,
        ringDamage = 30, innerDamage = 32, recovery = 0.26f,
    };
    [SerializeField] private CoilSettings coilPhase2 = new CoilSettings
    {
        ringInner = 1.75f, ringOuter = 4.7f, innerRadius = 2.05f,
        firstTelegraph = 0.4f, secondTelegraph = 0.36f, betweenStrikes = 0.42f,
        ringDamage = 34, innerDamage = 36, recovery = 0.2f,
    };

    [Header("접촉 피해 — 노출 상태에서만")]
    [SerializeField, Min(0)] private int contactDamage = 18;
    [SerializeField, Min(0f)] private float contactInterval = 1f;

    [Header("페이즈 전환")]
    [Tooltip("포효하며 물결이 퍼지는 연출 시간. 피해는 없다.")]
    [SerializeField, Min(0f)] private float phaseRoarDuration = 0.8f;

    [Header("카메라")]
    [Tooltip("위·아래 하이드로펌프 원점과 잠항 표시가 화면에 들어오도록 넓히는 값. 0이면 그대로 둔다.")]
    [SerializeField, Min(0f)] private float arenaCameraSize = 6.8f;

    [Header("디버그")]
    [Tooltip("상태·패턴·해류·경로를 콘솔에 남긴다. 수치를 조정할 때만 켠다.")]
    [SerializeField] private bool logPatterns;

    [Header("연출 색상")]
    [SerializeField] private Color currentArrowColor = new Color(0.08f, 0.28f, 0.62f, 0.85f);
    [SerializeField] private Color laneBoundaryColor = new Color(0.9f, 0.98f, 1f, 0.5f);
    [SerializeField] private Color warningColor = new Color(0.85f, 0.1f, 0.28f, 0.55f);
    [SerializeField] private Color beamColor = new Color(0.3f, 0.75f, 1f, 0.9f);
    [SerializeField] private Color splashColor = new Color(0.85f, 0.97f, 1f, 0.7f);
    [SerializeField] private Color floodColor = new Color(0.1f, 0.35f, 0.75f, 0.6f);
    [SerializeField] private Color foamColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private Color summonMarkerColor = new Color(0.06f, 0.2f, 0.55f, 0.8f);
    [SerializeField] private Color diveMarkerColor = new Color(0.05f, 0.12f, 0.3f, 0.75f);
    [SerializeField] private Color eyeGlowColor = new Color(1f, 0.85f, 0.3f, 0.9f);

    // ---------------------------------------------------------------- 런타임 상태

    private State state = State.Intro;
    private bool inPhase2;
    private bool phaseTransitionPending;
    private bool phaseTransitionDone;
    private int stateInvulnerabilityLocks;

    private EnemyController enemyController;
    private EnemyAnimator enemyAnimator;
    private Health health;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Health playerHealth;
    private Camera arenaCamera;
    private float cameraSizeBeforeFight;
    /// <summary>전투 동안 꺼 두는 픽셀 퍼펙트 카메라. 나갈 때 반드시 되돌린다.</summary>
    private Behaviour pixelPerfectCamera;

    private Vector2 fallbackArenaCenter;
    private float nextContactDamageTime;

    /// <summary>보스가 만든 모든 공격 오브젝트의 부모. 보스 배율을 따라가지 않게 씬 루트에 둔다.</summary>
    private Transform attackRoot;
    /// <summary>잉어킹의 부모. 공격 오브젝트와 수명이 달라 따로 둔다.</summary>
    private Transform summonRoot;
    private WaterCurrentField currentField;

    private readonly List<MagikarpObstacle> magikarps = new List<MagikarpObstacle>(4);
    private readonly List<Collider2D> ownColliders = new List<Collider2D>();
    private readonly List<GyaradosFloodZone> activeFloods = new List<GyaradosFloodZone>(3);
    /// <summary>기준점이 비어 자동 계산으로 넘어간 발사 면. 경고를 면마다 한 번만 남기려고 둔다.</summary>
    private readonly HashSet<GyaradosHydroBeam.HydroFace> missingHydroOrigins =
        new HashSet<GyaradosHydroBeam.HydroFace>();

    /// <summary>상태·페이즈·사망으로 예약된 공격을 무효화하는 세대 번호.</summary>
    private int attackGeneration;

    // 셔플 백. 외부·내부 패턴을 따로 관리한다.
    private readonly List<OuterPattern> outerBag = new List<OuterPattern>(2);
    private readonly List<InnerPattern> innerBag = new List<InnerPattern>(2);
    private bool hasLastOuter;
    private OuterPattern lastOuter;
    private bool hasLastInner;
    private InnerPattern lastInner;
    /// <summary>첫 전투의 첫 내부 패턴은 똬리치기로 고정한다.</summary>
    private bool firstInnerPattern = true;

    private float exposedEndTime;

    // ---------------------------------------------------------------- 기준점

    private Vector2 ArenaCenter => arenaBounds != null ? (Vector2)arenaBounds.position : fallbackArenaCenter;

    private Vector2 ArenaHalfSize => arenaBounds != null
        ? new Vector2(Mathf.Abs(arenaBounds.localScale.x) * 0.5f, Mathf.Abs(arenaBounds.localScale.y) * 0.5f)
        : arenaHalfSize;

    private Vector2 ExposedPosition => exposedAnchor != null ? (Vector2)exposedAnchor.position : ArenaCenter;

    private Vector2 PlayerPosition => player != null ? (Vector2)player.position : ArenaCenter;

    private CurrentSettings Currents => inPhase2 ? currentPhase2 : currentPhase1;
    private HydroSettings Hydro => inPhase2 ? hydroPhase2 : hydroPhase1;
    private FloodSettings Flood => inPhase2 ? floodPhase2 : floodPhase1;
    private SummonSettings Summon => inPhase2 ? summonPhase2 : summonPhase1;
    private CoilSettings Coil => inPhase2 ? coilPhase2 : coilPhase1;

    /// <summary>기준점이 비어 있을 때 쓰는 잠항 위치. 전투 영역 밖 2.2유닛에 둔다.</summary>
    private Vector2 FallbackDivePosition(Vector2 outward)
    {
        Vector2 half = ArenaHalfSize;
        return ArenaCenter + new Vector2(outward.x * (half.x + 2.2f), outward.y * (half.y + 2.2f));
    }

    private Vector2 DivePosition(Transform anchor, Vector2 outward) =>
        anchor != null ? (Vector2)anchor.position : FallbackDivePosition(outward);

    /// <summary>물대포가 튕기는 네 벽의 안쪽 면. 기준점이 없으면 전투 영역으로 물러선다.</summary>
    private Vector2 ReflectCenter =>
        hydroReflectBounds != null ? (Vector2)hydroReflectBounds.position : ArenaCenter;

    private Vector2 ReflectHalfSize => hydroReflectBounds != null
        ? new Vector2(Mathf.Abs(hydroReflectBounds.localScale.x) * 0.5f,
                      Mathf.Abs(hydroReflectBounds.localScale.y) * 0.5f)
        : ArenaHalfSize;

    private Transform HydroOriginAnchor(GyaradosHydroBeam.HydroFace face)
    {
        switch (face)
        {
            case GyaradosHydroBeam.HydroFace.Top: return hydroOriginTop;
            case GyaradosHydroBeam.HydroFace.Bottom: return hydroOriginBottom;
            case GyaradosHydroBeam.HydroFace.Left: return hydroOriginLeft;
            default: return hydroOriginRight;
        }
    }

    /// <summary>
    /// 그 면의 발사 원점. 기준점이 비어 있으면 반사 경계 바깥으로 자동 계산하고 한 번만 경고한다 —
    /// 자동 계산은 방을 꾸미다 만 상태에서도 전투가 굴러가게 하는 예비 처리일 뿐이다.
    /// </summary>
    private Vector2 HydroOrigin(GyaradosHydroBeam.HydroFace face)
    {
        Transform anchor = HydroOriginAnchor(face);
        if (anchor != null) return anchor.position;

        if (missingHydroOrigins.Add(face))
            Debug.LogWarning("[갸라도스] HydroOrigin_" + face + " 기준점이 비어 있다 — 경계 밖으로 자동 계산한다", this);

        Vector2 outward = -GyaradosHydroBeam.InwardNormal(face);
        Vector2 half = ReflectHalfSize;
        return ReflectCenter + new Vector2(outward.x * (half.x + 1.6f), outward.y * (half.y + 1.6f));
    }

    // ---------------------------------------------------------------- 수명 주기

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        fallbackArenaCenter = transform.parent != null
            ? (Vector2)transform.parent.position : (Vector2)transform.position;

        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
            if (collider != null && !collider.isTrigger) ownColliders.Add(collider);

        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;
    }

    private void OnEnable()
    {
        // 기본 추적 AI와 이 컨트롤러가 동시에 Rigidbody를 만지면 안 된다.
        enemyController.SetBasicAIEnabled(false);
    }

    private void OnDisable()
    {
        EndAllStateInvulnerability();
        RestoreCamera();
        if (currentField != null) currentField.StopField();
        if (enemyController != null) enemyController.SetBasicAIEnabled(true);
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
        }

        // 보스의 배율(0.85)을 따라가지 않도록 씬 루트에 둔다.
        attackRoot = new GameObject("Gyarados_Attacks").transform;
        summonRoot = new GameObject("Gyarados_Summons").transform;

        currentField = new GameObject("Gyarados_Currents").AddComponent<WaterCurrentField>();
        currentField.Configure(ArenaCenter, ArenaHalfSize, arrowsPerLane, arrowScrollSpeed,
                               currentArrowColor, laneBoundaryColor);
        currentField.SetTuning(Currents.speed, Currents.minHold, Currents.maxHold, Currents.telegraph);
        currentField.ChangeTelegraphStarted += HandleCurrentTelegraph;
        currentField.Changed += HandleCurrentChanged;

        WarnAboutLaneAnchors();
        ApplyArenaCamera();

        StartCoroutine(Battle());
    }

    private void OnDestroy()
    {
        EndAllStateInvulnerability();
        RestoreCamera();

        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
        if (currentField != null)
        {
            currentField.ChangeTelegraphStarted -= HandleCurrentTelegraph;
            currentField.Changed -= HandleCurrentChanged;
        }

        // 방을 넘어갈 때 공격 오브젝트가 씬에 남으면 안 된다.
        if (!gameObject.scene.isLoaded) return;
        if (attackRoot != null) Destroy(attackRoot.gameObject);
        if (summonRoot != null) Destroy(summonRoot.gameObject);
        if (currentField != null) Destroy(currentField.gameObject);
    }

    /// <summary>패턴 도중에 밀려나지 않게 한다. 갸라도스는 넉백과 경직에도 면역이다.</summary>
    private void Update()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    private void HandleDamaged()
    {
        if (phaseTransitionPending || phaseTransitionDone) return;
        if (health.CurrentHealth > health.MaxHealth * 0.5f) return;
        // 실행 중인 내부 패턴은 정상적으로 끝낸다. 여기서는 요청만 기록한다.
        phaseTransitionPending = true;
        Trace("페이즈 전환 요청 (체력 " + health.CurrentHealth + "/" + health.MaxHealth + ")");
    }

    private void HandleDied()
    {
        StopAllCoroutines();
        EndAllStateInvulnerability();
        ClearAttackObjects();
        ClearMagikarps();
        if (currentField != null) currentField.StopField();
        RestoreCamera();
        SetBossVisible(true);
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (enemyAnimator != null) enemyAnimator.ClearActionState();
        // 죽은 뒤 남은 공격 오브젝트가 플레이어를 때리거나 방 클리어를 막으면 안 된다.
        if (attackRoot != null) Destroy(attackRoot.gameObject);
        if (summonRoot != null) Destroy(summonRoot.gameObject);
        if (currentField != null) Destroy(currentField.gameObject);
    }

    /// <summary>접촉 피해는 노출 상태에서만 들어간다. 잠항·진입·이탈 중에는 충돌체 자체가 꺼져 있다.</summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (state != State.Exposed || health.IsDead || contactDamage <= 0) return;
        if (Time.time < nextContactDamageTime) return;
        if (collision.collider.GetComponentInParent<PlayerController>() == null) return;
        if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return;

        playerHealth.TakeDamage(contactDamage);
        nextContactDamageTime = Time.time + contactInterval;
    }

    // ---------------------------------------------------------------- 상태 기계

    private IEnumerator Battle()
    {
        yield return IntroRoutine();

        while (!health.IsDead)
        {
            yield return ExposedRoutine();
            if (health.IsDead) yield break;

            if (phaseTransitionPending && !phaseTransitionDone)
            {
                phaseTransitionDone = true;
                yield return PhaseTransitionRoutine();
            }
            else
            {
                yield return ExitRoutine();
            }
            if (health.IsDead) yield break;

            yield return SubmergedRoutine();
            if (health.IsDead) yield break;

            yield return EnterRoutine();
        }
    }

    /// <summary>
    /// 전투는 노출로 시작한다. 처음부터 긴 무적 구간을 보여 주지 않고,
    /// "이 보스는 때릴 수 있다"부터 알려 준다.
    /// </summary>
    private IEnumerator IntroRoutine()
    {
        state = State.Intro;
        Trace("Intro 시작");
        SetBossPosition(ExposedPosition);
        SetBossVisible(true);
        SetBossCollision(true);
        BeginStateInvulnerability();

        AttackTelegraph roar = AttackTelegraph.CreateRing(attackRoot, transform.position, 0.8f, splashColor);
        roar.Expand(0.8f, ArenaHalfSize.x, introDuration);
        FaceTowardPlayer();

        yield return new WaitForSeconds(introDuration);
        if (enemyAnimator != null) enemyAnimator.ClearActionState();
        if (health.IsDead) yield break;

        // 삼중 해류는 Intro가 끝나는 순간부터 작동한다.
        currentField.Begin();
        Trace("해류 시작 " + currentField.SignText());
        EndStateInvulnerability();
    }

    /// <summary>
    /// 노출 상태. 정해진 시간 동안 내부 패턴을 반복한다. 받은 피해량은 이 시간을 바꾸지 않는다.
    /// 시간이 끝나도 실행 중인 패턴은 끊지 않고, 새 패턴만 시작하지 않는다.
    /// </summary>
    private IEnumerator ExposedRoutine()
    {
        state = State.Exposed;
        SetBossPosition(ExposedPosition);
        SetBossVisible(true);
        SetBossCollision(true);

        float duration = inPhase2 ? exposedDurationPhase2 : exposedDurationPhase1;
        exposedEndTime = Time.time + duration;
        Trace(string.Format("Exposed 진입 — {0}페이즈, 노출 {1:0.00}초", inPhase2 ? 2 : 1, duration));

        while (!health.IsDead)
        {
            float remaining = exposedEndTime - Time.time;
            if (remaining <= 0f)
            {
                Trace("Exposed 종료 — 시간 만료");
                break;
            }
            if (phaseTransitionPending && !phaseTransitionDone)
            {
                Trace("Exposed 종료 — 페이즈 전환 대기");
                break;
            }

            InnerPattern next = firstInnerPattern ? InnerPattern.Coil : DrawInner();
            float minimum = MinInnerDuration(next);
            if (remaining < minimum)
            {
                Trace(string.Format("Exposed 종료 — 남은 {0:0.00}초 < {1} 최소 {2:0.00}초",
                    remaining, next, minimum));
                break;
            }

            if (firstInnerPattern)
            {
                // 셔플 백은 두 번째 패턴부터 쓴다. 첫 패턴은 고정이므로 직전 기록만 남긴다.
                firstInnerPattern = false;
                hasLastInner = true;
            }
            lastInner = next;
            Trace(string.Format("  내부 패턴 {0} 시작 (남은 노출 {1:0.00}초, 백 {2})",
                next, remaining, InnerBagText()));

            if (next == InnerPattern.Summon) yield return SummonRoutine();
            else yield return CoilRoutine();

            if (health.IsDead) yield break;
            yield return new WaitForSeconds(inPhase2 ? innerGapPhase2 : innerGapPhase1);
        }
    }

    /// <summary>이탈. 큰 물보라를 남기고 바깥쪽 바다로 잠수한다.</summary>
    private IEnumerator ExitRoutine()
    {
        state = State.Exit;
        BeginStateInvulnerability();
        SetBossCollision(false);
        Trace("Exit 시작");

        Vector2 from = transform.position;
        Vector2 to = PickDiveAnchor(from);

        AttackTelegraph splash = AttackTelegraph.CreateRing(attackRoot, from, 0.6f, splashColor);
        splash.Expand(0.6f, 2.6f, Mathf.Max(0.1f, exitDuration));

        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < exitDuration && !health.IsDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, exitDuration));
            SetBossPosition(Vector2.Lerp(from, to, t));
            transform.localScale = baseScale * Mathf.Lerp(1f, 0.55f, t);
            yield return null;
        }

        transform.localScale = baseScale;
        SetBossPosition(to);
        SetBossVisible(false);
    }

    /// <summary>
    /// 잠항. 정해진 <b>패턴 수</b>를 모두 쓰면 끝난다. 시간으로 끝내지 않는다.
    /// 1페이즈는 하나, 2페이즈는 두 패턴을 무작위 순서로 모두 쓴다.
    /// </summary>
    private IEnumerator SubmergedRoutine()
    {
        state = State.Submerged;
        BeginStateInvulnerability();
        SetBossCollision(false);
        SetBossVisible(false);

        int count = Mathf.Max(1, inPhase2 ? outerPatternsPhase2 : outerPatternsPhase1);
        List<OuterPattern> order = DrawOuterOrder(count);
        Trace("Submerged 진입 — 외부 패턴 " + count + "회: " + string.Join(", ", order));

        for (int i = 0; i < order.Count && !health.IsDead; i++)
        {
            OuterPattern pattern = order[i];
            lastOuter = pattern;
            hasLastOuter = true;

            if (pattern == OuterPattern.HydroPump) yield return HydroPumpRoutine();
            else yield return CrushRoutine();

            if (health.IsDead) yield break;
            if (i < order.Count - 1) yield return new WaitForSeconds(outerGapPhase2);
        }
        Trace("Submerged 종료");
    }

    /// <summary>진입. 올라올 자리를 원형 물결로 먼저 보여 주고, 그 위로 솟아오른다.</summary>
    private IEnumerator EnterRoutine()
    {
        state = State.Enter;
        BeginStateInvulnerability();
        SetBossCollision(false);

        Vector2 target = PickEnterPosition();
        AttackTelegraph marker = AttackTelegraph.CreateRing(attackRoot, target, 1.4f, splashColor);
        marker.Pulse(enterDuration);
        Trace(string.Format("Enter 시작 — ({0:0.00}, {1:0.00})", target.x, target.y));

        Vector2 from = transform.position;
        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;
        bool revealed = false;

        while (elapsed < enterDuration && !health.IsDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, enterDuration));
            // 물결만 보여 주다가 후반부에 몸이 솟아오른다.
            if (!revealed && t >= 0.45f)
            {
                revealed = true;
                SetBossVisible(true);
            }
            if (revealed)
            {
                float rise = Mathf.InverseLerp(0.45f, 1f, t);
                SetBossPosition(Vector2.Lerp(from, target, rise));
                transform.localScale = baseScale * Mathf.Lerp(0.55f, 1f, rise);
            }
            yield return null;
        }

        transform.localScale = baseScale;
        SetBossPosition(target);
        SetBossVisible(true);
        SetBossCollision(true);
        // 진입 무적 + 잠항 무적 + (있다면) 이탈 무적을 한 번에 푼다.
        EndAllStateInvulnerability();
    }

    /// <summary>
    /// 페이즈 전환. 새 패턴을 추가하지 않고 같은 규칙을 강화한다.
    /// 노출 시간에 포함하지 않으며, 전환 뒤에는 곧바로 잠항으로 들어간다.
    /// </summary>
    private IEnumerator PhaseTransitionRoutine()
    {
        state = State.Exit;
        BeginStateInvulnerability();
        // 똬리치기 판정과 예고, 남은 잉어킹을 모두 지운다.
        ClearAttackObjects();
        ClearMagikarps();
        if (enemyAnimator != null) enemyAnimator.ClearActionState();
        Trace("페이즈 전환 실행");

        AttackTelegraph wave = AttackTelegraph.CreateRing(attackRoot, transform.position, 0.8f, splashColor);
        wave.Expand(0.8f, ArenaHalfSize.x + ArenaHalfSize.y, Mathf.Max(0.1f, phaseRoarDuration));

        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < phaseRoarDuration && !health.IsDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, phaseRoarDuration));
            transform.localScale = baseScale * Mathf.Lerp(1f, 1.15f, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        transform.localScale = baseScale;
        if (health.IsDead) yield break;

        inPhase2 = true;
        // 해류는 멈추지 않는다. 방향만 즉시 새로 뽑고 2페이즈 유지 시간 범위를 적용한다.
        currentField.SetTuning(Currents.speed, Currents.minHold, Currents.maxHold, Currents.telegraph);
        currentField.ForceChangeNow();
        Trace("2페이즈 해류 " + currentField.SignText());

        // 두 셔플 백을 모두 비운다.
        outerBag.Clear();
        innerBag.Clear();
        hasLastOuter = false;
        hasLastInner = false;

        yield return ExitRoutine();
    }

    // ---------------------------------------------------------------- 패턴 선택

    private List<OuterPattern> DrawOuterOrder(int count)
    {
        List<OuterPattern> order = new List<OuterPattern>(count);
        for (int i = 0; i < count; i++) order.Add(DrawOuter());

        // 직전 잠항의 마지막 패턴과 이번 잠항의 첫 패턴이 같으면 순서를 바꾼다.
        if (hasLastOuter && order.Count > 1 && order[0] == lastOuter)
            (order[0], order[1]) = (order[1], order[0]);
        return order;
    }

    private OuterPattern DrawOuter()
    {
        if (outerBag.Count == 0)
        {
            outerBag.Add(OuterPattern.HydroPump);
            outerBag.Add(OuterPattern.Crush);
            if (Random.value < 0.5f) (outerBag[0], outerBag[1]) = (outerBag[1], outerBag[0]);
            // 꺼내는 쪽은 리스트의 끝이다. 같은 패턴이 연달아 나오지 않게 마지막 칸을 살핀다.
            if (hasLastOuter && outerBag[1] == lastOuter)
                (outerBag[0], outerBag[1]) = (outerBag[1], outerBag[0]);
        }

        int index = outerBag.Count - 1;
        OuterPattern next = outerBag[index];
        outerBag.RemoveAt(index);
        return next;
    }

    /// <summary>
    /// 다음 내부 패턴. 셔플 백을 <see cref="coilPerInnerBag"/>:<see cref="summonPerInnerBag"/>로
    /// 채운다.
    ///
    /// 반씩 넣으면 <b>횟수</b>는 같아도 화면에 나오는 <b>시간</b>은 똬리치기가 두 배 넘게 길다 —
    /// 똬리치기는 두 번 때리느라 예고·간격·후딜을 합쳐 2초에 가깝고, 잉어킹 소환은 1초가 채 안
    /// 된다. 그래서 "똬리치기만 한다"고 느껴진다. 소환을 더 많이 넣어 시간 비중을 맞춘다.
    /// </summary>
    private InnerPattern DrawInner()
    {
        if (innerBag.Count == 0)
        {
            for (int i = 0; i < coilPerInnerBag; i++) innerBag.Add(InnerPattern.Coil);
            for (int i = 0; i < summonPerInnerBag; i++) innerBag.Add(InnerPattern.Summon);
            Shuffle(innerBag);

            // 꺼내는 쪽은 리스트의 끝이다. 직전과 같은 패턴이 연달아 나오지 않게, 다른 패턴이
            // 백에 있으면 끝으로 끌어온다.
            int last = innerBag.Count - 1;
            if (hasLastInner && innerBag[last] == lastInner)
                for (int i = 0; i < last; i++)
                    if (innerBag[i] != lastInner)
                    {
                        (innerBag[i], innerBag[last]) = (innerBag[last], innerBag[i]);
                        break;
                    }
        }

        int index = innerBag.Count - 1;
        InnerPattern next = innerBag[index];
        innerBag.RemoveAt(index);
        hasLastInner = true;
        return next;
    }

    private static void Shuffle(List<InnerPattern> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private string InnerBagText() => innerBag.Count == 0 ? "비어 있음" : string.Join(", ", innerBag);

    /// <summary>패턴이 판정과 후딜까지 끝내는 데 최소로 걸리는 시간. 남은 노출 시간과 견준다.</summary>
    private float MinInnerDuration(InnerPattern pattern)
    {
        if (pattern == InnerPattern.Summon)
        {
            SummonSettings s = Summon;
            return s.telegraph + s.recovery;
        }

        CoilSettings c = Coil;
        return c.firstTelegraph + Mathf.Max(MinStrikeInterval, c.betweenStrikes + c.secondTelegraph)
               + c.recovery;
    }

    // ---------------------------------------------------------------- 잠항 패턴 1 · 하이드로펌프

    /// <summary>
    /// 외곽 바다의 네 방향 중 한 곳에서 굵은 물대포를 쏜다. 물대포는 전투장 <b>벽</b>에 닿을 때마다
    /// 입사각과 같은 각도로 튕긴다.
    ///
    /// 발사는 즉발이다. 예고가 끝나는 순간 꺾인 경로 <b>전체</b>가 한꺼번에 번쩍이며 동시에 판정을 낸다.
    /// 물줄기가 날아오는 것을 보고 피할 여지는 없고, 예고선이 떠 있는 동안 반사 경로를 미리 읽어
    /// 비켜서 있어야 한다. 그래서 예고 시간이 이 패턴의 유일한 회피 여유다 — 줄일 때 주의할 것.
    ///
    /// 플레이어에게 보여 주는 예고는 최초 발사 방향뿐이다. 반사 이후 경로는 미리 그리지 않고,
    /// "벽에서 똑같은 각도로 튕긴다"는 규칙 하나로 읽게 한다.
    /// </summary>
    private IEnumerator HydroPumpRoutine()
    {
        HydroSettings settings = Hydro;

        // 네 발사 면을 같은 확률로 고른다.
        GyaradosHydroBeam.HydroFace face = (GyaradosHydroBeam.HydroFace)Random.Range(0, 4);
        Vector2 origin = HydroOrigin(face);

        // 외곽 바다에 갸라도스의 그림자와 눈빛을 띄워 어디서 오는지 알린다.
        GameObject marker = CreateDiveMarker(origin);

        List<Vector2> reflectionPoints = new List<Vector2>(2);
        List<GyaradosHydroBeam.Segment> path =
            PlanHydroPath(origin, face, settings, reflectionPoints, out Vector2 entry);

        if (path == null)
        {
            // 무한 재시도하지 않는다. 짧은 후딜만 두고 다음 상태로 넘어간다.
            Trace("  하이드로펌프 경로 실패 — 패턴을 취소한다");
            if (marker != null) Destroy(marker);
            yield return new WaitForSeconds(settings.recovery);
            yield break;
        }

        Trace(string.Format(
            "  하이드로펌프 {0} 원점({1:0.00}, {2:0.00}) 첫 방향 {3} 진입({4:0.00}, {5:0.00}) 반사 {6}회",
            FaceName(face), origin.x, origin.y, Format(path[0]), entry.x, entry.y, settings.reflections));
        foreach (Vector2 point in reflectionPoints)
            Trace(string.Format("    반사점 ({0:0.00}, {1:0.00})", point.x, point.y));

        // 예고선은 발사 원점부터 첫 번째 반사 지점까지만 보여 준다.
        GyaradosHydroBeam.Segment first = path[0];
        AttackTelegraph line = AttackTelegraph.CreateLine(
            attackRoot, first.A, first.Direction, first.Length, settings.width, warningColor);
        line.Pulse(settings.telegraph);

        yield return new WaitForSeconds(settings.telegraph);
        if (marker != null) Destroy(marker);
        if (health.IsDead) yield break;

        // 즉발이다 — 경로 전체가 이 순간 한꺼번에 번쩍이며 판정을 낸다.
        GyaradosHydroBeam beam = GyaradosHydroBeam.Launch(attackRoot, path,
            settings.width, settings.trailDuration, settings.damage, beamColor, splashColor);

        // 번쩍임이 사라진 뒤에 다음 외부 패턴으로 넘어간다.
        int generation = attackGeneration;
        while (beam != null && !beam.IsFinished && generation == attackGeneration && !health.IsDead)
            yield return null;

        if (health.IsDead) yield break;
        yield return new WaitForSeconds(settings.recovery);
    }

    /// <summary>
    /// 발사 경로를 미리 시뮬레이션해 페이즈가 요구하는 반사 횟수를 채우는 후보만 고른다.
    ///
    /// 1. 예고 시작 시점의 플레이어 위치를 겨눈다. 이후 추적하지 않는다.
    /// 2. 실패하면 조준점을 전투장 중심 쪽으로 당겨 가며 다시 시도한다.
    /// 3. 그래도 실패하면 발사 면 정면에서 좌우로 기울인 예비 각도를 쓴다. 정면을 그대로 겨누면
    ///    반대쪽 벽에 수직으로 부딪혀 왔던 길을 되짚어 오므로, 기운 각도만 후보로 둔다.
    ///
    /// 마지막 예비 각도까지 실패하면 <c>null</c>이다. 부르는 쪽은 패턴을 취소한다.
    /// </summary>
    private List<GyaradosHydroBeam.Segment> PlanHydroPath(Vector2 origin, GyaradosHydroBeam.HydroFace face,
                                                          HydroSettings settings,
                                                          List<Vector2> reflectionPoints, out Vector2 entry)
    {
        Vector2 center = ReflectCenter;
        Vector2 aim = PlayerPosition;
        int steps = Mathf.Max(2, hydroAimAttempts);

        for (int attempt = 0; attempt < steps; attempt++)
        {
            // 마지막 시도가 정확히 전투장 중심이 되도록 나눈다.
            float shrink = 1f - attempt / (float)(steps - 1);
            Vector2 target = center + (aim - center) * shrink;
            List<GyaradosHydroBeam.Segment> path =
                TryHydroPath(origin, target - origin, face, settings, reflectionPoints, out entry);
            if (path == null) continue;

            if (attempt > 0)
                Trace(string.Format("    조준 후보 {0}회 만에 성공 (조준 ({1:0.00}, {2:0.00}))",
                    attempt + 1, target.x, target.y));
            return path;
        }

        // 예비 각도. 면 정면에서 좌우로 기울여 가며 찾는다.
        Vector2 straight = center - origin;
        foreach (float tilt in HydroFallbackTilts)
        {
            List<GyaradosHydroBeam.Segment> path = TryHydroPath(
                origin, Rotate(straight, tilt), face, settings, reflectionPoints, out entry);
            if (path == null) continue;

            Trace(string.Format("    조준 실패 — 예비 각도 {0:0}도 사용", tilt));
            return path;
        }

        entry = origin;
        return null;
    }

    private List<GyaradosHydroBeam.Segment> TryHydroPath(Vector2 origin, Vector2 direction,
                                                         GyaradosHydroBeam.HydroFace face,
                                                         HydroSettings settings,
                                                         List<Vector2> reflectionPoints, out Vector2 entry) =>
        GyaradosHydroBeam.BuildPath(origin, direction, ReflectCenter, ReflectHalfSize, face,
                                    settings.reflections, ReflectionEpsilon, settings.cornerMargin,
                                    settings.minSegmentLength, out entry, reflectionPoints);

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private static string FaceName(GyaradosHydroBeam.HydroFace face)
    {
        switch (face)
        {
            case GyaradosHydroBeam.HydroFace.Top: return "위";
            case GyaradosHydroBeam.HydroFace.Bottom: return "아래";
            case GyaradosHydroBeam.HydroFace.Left: return "왼쪽";
            default: return "오른쪽";
        }
    }

    private static string Format(GyaradosHydroBeam.Segment segment)
    {
        Vector2 d = segment.Direction;
        return string.Format("({0:0.00}, {1:0.00})", d.x, d.y);
    }

    // ---------------------------------------------------------------- 잠항 패턴 2 · 격류 압착

    /// <summary>
    /// 전투장 가장자리에서 물이 차오르며 설 자리를 사분면 단위로 압축한다.
    /// 범람은 플레이어를 밀지 않는다 — 강제 이동은 삼중 해류 하나로 통일한다.
    /// </summary>
    private IEnumerator CrushRoutine()
    {
        FloodSettings settings = Flood;

        FloodSide first = (FloodSide)Random.Range(0, 4);
        FloodSide second = PerpendicularOf(first);
        Trace("  격류 압착 " + first + " → " + second + (inPhase2 ? " → " + Opposite(first) : ""));

        GyaradosFloodZone firstZone = SpawnFlood(first, settings.depthRatio);
        yield return new WaitForSeconds(settings.telegraph);
        if (health.IsDead) { ClearFloods(); yield break; }
        ActivateFlood(firstZone, settings);

        GyaradosFloodZone secondZone = SpawnFlood(second, settings.depthRatio);
        yield return new WaitForSeconds(settings.telegraph);
        if (health.IsDead) { ClearFloods(); yield break; }
        ActivateFlood(secondZone, settings);

        yield return new WaitForSeconds(settings.holdAfterSecond);
        if (health.IsDead) { ClearFloods(); yield break; }

        if (inPhase2)
        {
            // 세 번째 예고를 시작할 때 첫 번째 범람을 먼저 걷어 낸다. 두 번째는 유지한다.
            RemoveFlood(firstZone);
            FloodSide third = Opposite(first);
            GyaradosFloodZone thirdZone = SpawnFlood(third, settings.depthRatio);
            yield return new WaitForSeconds(settings.thirdTelegraph);
            if (health.IsDead) { ClearFloods(); yield break; }
            ActivateFlood(thirdZone, settings);

            yield return new WaitForSeconds(settings.holdAfterThird);
        }

        ClearFloods();
        if (health.IsDead) yield break;
        // 물이 전부 빠진 뒤에 다음 상태로 넘어간다.
        yield return new WaitForSeconds(settings.recovery);
    }

    private static FloodSide Opposite(FloodSide side)
    {
        switch (side)
        {
            case FloodSide.Left: return FloodSide.Right;
            case FloodSide.Right: return FloodSide.Left;
            case FloodSide.Bottom: return FloodSide.Top;
            default: return FloodSide.Bottom;
        }
    }

    /// <summary>첫 범람과 수직인 두 방향 중 하나. 반대편은 고르지 않는다.</summary>
    private static FloodSide PerpendicularOf(FloodSide side)
    {
        bool horizontal = side == FloodSide.Left || side == FloodSide.Right;
        if (horizontal) return Random.value < 0.5f ? FloodSide.Bottom : FloodSide.Top;
        return Random.value < 0.5f ? FloodSide.Left : FloodSide.Right;
    }

    private GyaradosFloodZone SpawnFlood(FloodSide side, float depthRatio)
    {
        Vector2 center = ArenaCenter;
        Vector2 half = ArenaHalfSize;
        float ratio = Mathf.Clamp01(depthRatio);

        Rect area;
        Vector2 inward;
        switch (side)
        {
            case FloodSide.Left:
                area = new Rect(center.x - half.x, center.y - half.y, half.x * 2f * ratio, half.y * 2f);
                inward = Vector2.right;
                break;
            case FloodSide.Right:
                area = new Rect(center.x + half.x - half.x * 2f * ratio, center.y - half.y,
                                half.x * 2f * ratio, half.y * 2f);
                inward = Vector2.left;
                break;
            case FloodSide.Bottom:
                area = new Rect(center.x - half.x, center.y - half.y, half.x * 2f, half.y * 2f * ratio);
                inward = Vector2.up;
                break;
            default:
                area = new Rect(center.x - half.x, center.y + half.y - half.y * 2f * ratio,
                                half.x * 2f, half.y * 2f * ratio);
                inward = Vector2.down;
                break;
        }

        GyaradosFloodZone zone = GyaradosFloodZone.Spawn(attackRoot, area, inward,
                                                         warningColor, floodColor, foamColor);
        activeFloods.Add(zone);
        return zone;
    }

    private void ActivateFlood(GyaradosFloodZone zone, FloodSettings settings)
    {
        if (zone == null) return;
        zone.Activate(settings.damage, settings.damageRetryInterval,
                      settings.slowMultiplier, settings.slowDuration, currentField);
    }

    private void RemoveFlood(GyaradosFloodZone zone)
    {
        if (zone == null) return;
        activeFloods.Remove(zone);
        zone.Recede();
    }

    private void ClearFloods()
    {
        for (int i = activeFloods.Count - 1; i >= 0; i--)
            if (activeFloods[i] != null) activeFloods[i].Recede();
        activeFloods.Clear();
    }

    // ---------------------------------------------------------------- 노출 패턴 1 · 잉어킹 소환

    /// <summary>
    /// 잉어킹을 불러 고정 장애물로 세운다. 플레이어를 돕거나 공격하지 않고, 이동 경로와
    /// 때릴 자리만 좁힌다. 배치 규칙을 만족하는 자리가 부족하면 억지로 만들지 않고 수를 줄인다.
    /// </summary>
    private IEnumerator SummonRoutine()
    {
        SummonSettings settings = Summon;

        // 지난 소환에서 살아남은 잉어킹을 먼저 치운다.
        ClearMagikarps();

        List<Vector2> candidates = BuildSummonCandidates();
        List<Vector2> accepted = new List<Vector2>(settings.count);
        int rejected = 0;

        for (int i = 0; i < candidates.Count && accepted.Count < settings.count; i++)
        {
            if (IsValidSummonSpot(candidates[i], accepted, settings.bodyRadius)) accepted.Add(candidates[i]);
            else rejected++;
        }

        Trace(string.Format("  잉어킹 소환 — 후보 {0}개, 채택 {1}/{2}, 거부 {3}개",
            candidates.Count, accepted.Count, settings.count, rejected));

        if (accepted.Count == 0)
        {
            yield return new WaitForSeconds(settings.recovery);
            yield break;
        }

        foreach (Vector2 spot in accepted)
        {
            // 물빛 바닥 위에서는 가는 테두리 하나로는 눈에 띄지 않는다. 속을 채운 원으로
            // 자리를 분명히 깔고, 그 위에 진한 테두리를 얹어 경계를 세운다.
            AttackTelegraph fill = AttackTelegraph.CreateCircle(
                attackRoot, spot, settings.bodyRadius, summonMarkerColor);
            fill.Pulse(settings.telegraph);

            Color edge = summonMarkerColor;
            edge.a = Mathf.Clamp01(summonMarkerColor.a * 1.6f);
            AttackTelegraph ripple = AttackTelegraph.CreateRing(
                attackRoot, spot, settings.bodyRadius * 1.15f, edge);
            ripple.Pulse(settings.telegraph);
        }

        yield return new WaitForSeconds(settings.telegraph);
        if (health.IsDead) yield break;

        foreach (Vector2 spot in accepted) SpawnMagikarp(spot, settings);

        yield return new WaitForSeconds(settings.recovery);
    }

    /// <summary>
    /// 완전한 연속 무작위 대신 수로 중앙·수로 경계 부근·보스 접근 경로에 미리 만든 후보점을 섞어 쓴다.
    /// 그래야 "길을 좁힌다"는 뜻이 살아난다.
    /// </summary>
    private List<Vector2> BuildSummonCandidates()
    {
        Vector2 center = ArenaCenter;
        Vector2 half = ArenaHalfSize;
        float third = half.y / 3f;

        float[] offsetsX = { -0.78f, -0.52f, -0.26f, 0f, 0.26f, 0.52f, 0.78f };
        float[] rowsY = { -third * 2f, -third, 0f, third, third * 2f };

        List<Vector2> candidates = new List<Vector2>(offsetsX.Length * rowsY.Length);
        foreach (float ry in rowsY)
            foreach (float ox in offsetsX)
                candidates.Add(new Vector2(center.x + ox * half.x, center.y + ry));

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        return candidates;
    }

    /// <summary>배치 규칙 전부를 한 번에 검사한다. 하나라도 어기면 그 자리는 버린다.</summary>
    private bool IsValidSummonSpot(Vector2 spot, List<Vector2> accepted, float radius)
    {
        Vector2 center = ArenaCenter;
        Vector2 half = ArenaHalfSize;

        // 벽 안쪽 면과의 거리. 잉어킹과 벽 사이에 지나갈 수 없는 가짜 통로를 만들지 않는다.
        float gapX = half.x - Mathf.Abs(spot.x - center.x);
        float gapY = half.y - Mathf.Abs(spot.y - center.y);
        float wallGap = Mathf.Min(gapX, gapY);
        if (wallGap < minDistanceFromWall) return false;
        if (wallGap - radius < safeCorridorWidth) return false;

        if (Vector2.Distance(spot, PlayerPosition) < minDistanceFromPlayer) return false;
        if (Vector2.Distance(spot, ExposedPosition) < minDistanceFromBoss) return false;

        foreach (Vector2 other in accepted)
            if (Vector2.Distance(spot, other) < minDistanceBetweenMagikarp) return false;

        // 세 수로 사이를 오가는 경계를 동시에 모두 막으면 안 된다.
        List<Vector2> withSpot = new List<Vector2>(accepted) { spot };
        if (BlocksEveryLaneBoundary(withSpot, radius)) return false;
        // 갸라도스 주변을 완전히 둘러싸도 안 된다.
        if (SurroundsBoss(withSpot, radius)) return false;

        return true;
    }

    /// <summary>두 수로 경계가 모두 막히는지. 한 곳이라도 지나갈 틈이 남으면 통과다.</summary>
    private bool BlocksEveryLaneBoundary(List<Vector2> spots, float radius)
    {
        for (int i = 0; i < 2; i++)
        {
            float boundaryY = ArenaCenter.y + (i == 0 ? -ArenaHalfSize.y / 3f : ArenaHalfSize.y / 3f);
            if (!BoundaryBlocked(spots, radius, boundaryY)) return false;
        }
        return true;
    }

    /// <summary>그 경계선 위에 플레이어가 지나갈 만한 틈이 남아 있는지 본다.</summary>
    private bool BoundaryBlocked(List<Vector2> spots, float radius, float boundaryY)
    {
        float playerHalf = safeCorridorWidth * 0.5f;
        float left = ArenaCenter.x - ArenaHalfSize.x;
        float right = ArenaCenter.x + ArenaHalfSize.x;

        // 경계선 근처에 있는 잉어킹이 가리는 X 구간을 모아 정렬한다.
        List<Vector2> blocked = new List<Vector2>(spots.Count);
        foreach (Vector2 spot in spots)
        {
            float reach = radius + playerHalf;
            if (Mathf.Abs(spot.y - boundaryY) > reach) continue;
            blocked.Add(new Vector2(spot.x - reach, spot.x + reach));
        }
        blocked.Sort((a, b) => a.x.CompareTo(b.x));

        float cursor = left;
        foreach (Vector2 span in blocked)
        {
            if (span.x - cursor >= safeCorridorWidth) return false;
            cursor = Mathf.Max(cursor, span.y);
        }
        return right - cursor < safeCorridorWidth;
    }

    /// <summary>갸라도스 주위에 빠져나갈 각도가 남아 있는지. 남지 않으면 둘러싼 것으로 본다.</summary>
    private bool SurroundsBoss(List<Vector2> spots, float radius)
    {
        Vector2 boss = ExposedPosition;
        float ring = minDistanceFromBoss + radius + safeCorridorWidth;

        List<float> angles = new List<float>(spots.Count);
        foreach (Vector2 spot in spots)
        {
            Vector2 offset = spot - boss;
            if (offset.magnitude > ring) continue;
            angles.Add(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        }
        if (angles.Count < 3) return false;

        angles.Sort();
        float widest = 360f - (angles[angles.Count - 1] - angles[0]);
        for (int i = 1; i < angles.Count; i++)
            widest = Mathf.Max(widest, angles[i] - angles[i - 1]);
        return widest < minEscapeAngle;
    }

    private void SpawnMagikarp(Vector2 spot, SummonSettings settings)
    {
        if (magikarpPrefab == null)
        {
            Trace("  잉어킹 프리팹이 비어 있다 — 소환을 건너뛴다");
            return;
        }

        MagikarpObstacle karp = Instantiate(magikarpPrefab, spot, Quaternion.identity, summonRoot);
        karp.Configure(settings.magikarpHealth, settings.bodyRadius);
        magikarps.Add(karp);
    }

    private void ClearMagikarps()
    {
        for (int i = magikarps.Count - 1; i >= 0; i--)
            if (magikarps[i] != null) magikarps[i].Remove();
        magikarps.Clear();
    }

    /// <summary>아직 살아 있는 잉어킹만 추린다. 플레이어가 부순 개체는 목록에서 뺀다.</summary>
    private void PruneMagikarps()
    {
        for (int i = magikarps.Count - 1; i >= 0; i--)
            if (magikarps[i] == null || !magikarps[i].IsAlive) magikarps.RemoveAt(i);
    }

    // ---------------------------------------------------------------- 노출 패턴 2 · 똬리치기

    /// <summary>
    /// 몸을 말고 바깥 고리와 안쪽 원을 차례로 때린다. 고리 공격 때는 갸라도스 <b>바로 옆</b>과
    /// 고리 바깥이 안전하므로, 계속 붙어서 때릴지 물러날지를 플레이어가 고른다.
    ///
    /// 1페이즈는 순서를 고정해 규칙을 가르치고, 2페이즈는 두 순서를 같은 확률로 섞는다.
    /// </summary>
    private IEnumerator CoilRoutine()
    {
        CoilSettings settings = Coil;
        bool ringFirst = !inPhase2 || Random.value < 0.5f;

        // 해류와 잉어킹 때문에 모든 탈출로가 막혔으면 패턴을 시작하지 않는다.
        EnsureCoilEscape(settings, ringFirst);

        Trace(string.Format("  똬리치기 {0} (고리 {1:0.00}~{2:0.00}, 원 {3:0.00})",
            ringFirst ? "고리 → 원" : "원 → 고리",
            settings.ringInner, settings.ringOuter, settings.innerRadius));

        Vector2 pivot = transform.position;
        FaceTowardPlayer();

        // 첫 공격
        yield return TelegraphCoil(pivot, ringFirst, settings, settings.firstTelegraph);
        if (health.IsDead) yield break;
        StrikeCoil(pivot, ringFirst, settings);

        // 두 번째 예고까지의 간격. 두 타격 사이가 플레이어 무적(0.5초)보다 짧으면 두 번째 판정이
        // 통째로 사라지므로, 예고 시간을 뺀 나머지로 하한을 지킨다.
        float gap = Mathf.Max(settings.betweenStrikes, MinStrikeInterval - settings.secondTelegraph);
        yield return new WaitForSeconds(gap);
        if (health.IsDead) yield break;

        yield return TelegraphCoil(pivot, !ringFirst, settings, settings.secondTelegraph);
        if (health.IsDead) yield break;
        StrikeCoil(pivot, !ringFirst, settings);

        if (enemyAnimator != null) enemyAnimator.ClearActionState();
        yield return new WaitForSeconds(settings.recovery);
    }

    /// <summary>예고는 실제 판정과 같은 모양·같은 크기로 그린다. 고리와 원은 서로 다른 모양이다.</summary>
    private IEnumerator TelegraphCoil(Vector2 pivot, bool ring, CoilSettings settings, float duration)
    {
        GameObject shape = ring
            ? CreateShape("CoilRingWarning", pivot, GyaradosShapes.Annulus(settings.ringInner / settings.ringOuter),
                          settings.ringOuter * 2f, warningColor, AttackTelegraph.SortingOrder)
            : CreateShape("CoilInnerWarning", pivot, PrimitiveSprites.Circle,
                          settings.innerRadius * 2f, warningColor, AttackTelegraph.SortingOrder);

        float elapsed = 0f;
        SpriteRenderer sr = shape.GetComponent<SpriteRenderer>();
        while (elapsed < duration && !health.IsDead)
        {
            elapsed += Time.deltaTime;
            float speed = Mathf.Lerp(4f, 12f, elapsed / Mathf.Max(0.01f, duration));
            Color c = warningColor;
            c.a = warningColor.a * (0.65f + 0.35f * Mathf.Sin(elapsed * speed));
            if (sr != null) sr.color = c;
            yield return null;
        }

        if (shape != null) Destroy(shape);
    }

    /// <summary>한 번의 고리·원 타격은 플레이어에게 최대 한 번만 들어간다.</summary>
    private void StrikeCoil(Vector2 pivot, bool ring, CoilSettings settings)
    {
        GameObject flash = ring
            ? CreateShape("CoilRing", pivot, GyaradosShapes.Annulus(settings.ringInner / settings.ringOuter),
                          settings.ringOuter * 2f, beamColor, AirborneSortingOrder)
            : CreateShape("CoilInner", pivot, PrimitiveSprites.Circle,
                          settings.innerRadius * 2f, beamColor, AirborneSortingOrder);
        Destroy(flash, 0.16f);

        if (player == null || playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return;

        float distance = Vector2.Distance(player.position, pivot);
        bool hit = ring
            ? distance >= settings.ringInner && distance <= settings.ringOuter
            : distance <= settings.innerRadius;
        if (hit) playerHealth.TakeDamage(ring ? settings.ringDamage : settings.innerDamage);
    }

    /// <summary>
    /// 첫 타격을 피할 자리가 남아 있는지 본다. 남아 있지 않으면 갸라도스와 가장 가까운
    /// 잉어킹을 하나 지우고 다시 본다 — 무작위 결과 때문에 피할 수 없는 상황을 만들지 않는다.
    /// </summary>
    private void EnsureCoilEscape(CoilSettings settings, bool ringFirst)
    {
        PruneMagikarps();

        while (!HasCoilEscape(settings, ringFirst))
        {
            MagikarpObstacle nearest = NearestMagikarpToBoss();
            if (nearest == null) return;
            Trace("    탈출로 없음 — 가장 가까운 잉어킹을 제거하고 다시 검사");
            magikarps.Remove(nearest);
            nearest.Remove();
        }
    }

    private bool HasCoilEscape(CoilSettings settings, bool ringFirst)
    {
        Vector2 pivot = transform.position;
        Vector2 center = ArenaCenter;
        Vector2 half = ArenaHalfSize;
        float margin = safeCorridorWidth * 0.5f;
        // 고리가 먼저면 고리 바깥으로, 원이 먼저면 원 바깥으로 빠지면 된다.
        float safeRadius = (ringFirst ? settings.ringOuter : settings.innerRadius) + margin;

        const int Samples = 24;
        for (int i = 0; i < Samples; i++)
        {
            float angle = i * (360f / Samples) * Mathf.Deg2Rad;
            Vector2 spot = pivot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * safeRadius;
            if (Mathf.Abs(spot.x - center.x) > half.x - margin) continue;
            if (Mathf.Abs(spot.y - center.y) > half.y - margin) continue;
            if (!ReachableByPlayer(spot, margin)) continue;
            return true;
        }

        // 고리 공격이면 갸라도스 바로 옆(안쪽 반지름 안)도 안전 구역이다.
        if (!ringFirst) return false;
        return Vector2.Distance(PlayerPosition, pivot) < settings.ringInner;
    }

    /// <summary>플레이어에서 그 자리까지 잉어킹에 막히지 않고 곧장 갈 수 있는지.</summary>
    private bool ReachableByPlayer(Vector2 spot, float playerHalf)
    {
        Vector2 from = PlayerPosition;
        foreach (MagikarpObstacle karp in magikarps)
        {
            if (karp == null || !karp.IsAlive) continue;
            float clearance = karp.BodyRadius + playerHalf;
            if (DistanceToSegment(karp.transform.position, from, spot) < clearance) return false;
        }
        return true;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared < 0.000001f) return Vector2.Distance(point, a);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        return Vector2.Distance(point, a + ab * t);
    }

    private MagikarpObstacle NearestMagikarpToBoss()
    {
        MagikarpObstacle nearest = null;
        float best = float.MaxValue;
        Vector2 pivot = transform.position;

        foreach (MagikarpObstacle karp in magikarps)
        {
            if (karp == null || !karp.IsAlive) continue;
            float distance = Vector2.Distance(karp.transform.position, pivot);
            if (distance >= best) continue;
            best = distance;
            nearest = karp;
        }
        return nearest;
    }

    // ---------------------------------------------------------------- 보조

    private GameObject CreateShape(string name, Vector2 position, Sprite sprite, float diameter,
                                   Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(attackRoot, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * diameter;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    /// <summary>외곽 바다에 뜨는 갸라도스의 그림자와 눈빛. 공격이 어디서 오는지 알려 준다.</summary>
    private GameObject CreateDiveMarker(Vector2 position)
    {
        GameObject shadow = CreateShape("DiveMarker", position, PrimitiveSprites.Circle, 2.4f,
                                        diveMarkerColor, AttackTelegraph.SortingOrder);

        for (int i = 0; i < 2; i++)
        {
            GameObject eye = new GameObject("Eye");
            eye.transform.SetParent(shadow.transform, false);
            eye.transform.position = position + new Vector2(i == 0 ? -0.35f : 0.35f, 0.15f);
            eye.transform.localScale = Vector3.one * (0.22f / 2.4f);
            SpriteRenderer sr = eye.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprites.Circle;
            sr.color = eyeGlowColor;
            sr.sortingOrder = AttackTelegraph.SortingOrder + 1;
        }
        return shadow;
    }

    /// <summary>이탈할 방향. 지금 자리에서 가장 가까운 잠항 기준점으로 빠진다.</summary>
    private Vector2 PickDiveAnchor(Vector2 from)
    {
        Vector2[] options =
        {
            DivePosition(diveAnchorTop, Vector2.up),
            DivePosition(diveAnchorBottom, Vector2.down),
            DivePosition(diveAnchorLeft, Vector2.left),
            DivePosition(diveAnchorRight, Vector2.right),
        };

        Vector2 best = options[0];
        float bestDistance = float.MaxValue;
        foreach (Vector2 option in options)
        {
            float distance = Vector2.Distance(from, option);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = option;
        }
        return best;
    }

    /// <summary>
    /// 올라올 자리. 기본은 전투장 중앙이고, 플레이어와 직접 겹치는 자리는 고르지 않는다.
    /// 패턴 가독성이 검증되기 전에는 무작위 위치로 바꾸지 않는다.
    /// </summary>
    private Vector2 PickEnterPosition()
    {
        Vector2 target = ExposedPosition;
        if (player == null) return target;

        Vector2 offset = target - PlayerPosition;
        if (offset.magnitude >= enterClearance) return target;

        Vector2 push = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right;
        Vector2 pushed = PlayerPosition + push * enterClearance;

        Vector2 center = ArenaCenter;
        Vector2 half = ArenaHalfSize;
        return new Vector2(
            Mathf.Clamp(pushed.x, center.x - half.x + 1f, center.x + half.x - 1f),
            Mathf.Clamp(pushed.y, center.y - half.y + 1f, center.y + half.y - 1f));
    }

    private void SetBossPosition(Vector2 position)
    {
        if (body != null) body.position = position;
        transform.position = position;
    }

    private void SetBossVisible(bool visible)
    {
        // 체력 바까지 함께 숨긴다. 잠항 중에는 몸이 전투 영역 밖에 있어야 한다.
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = visible;
    }

    private void SetBossCollision(bool solid)
    {
        foreach (Collider2D collider in ownColliders)
            if (collider != null) collider.enabled = solid;
    }

    private void BeginStateInvulnerability()
    {
        if (health == null || health.IsDead) return;
        health.BeginInvulnerability();
        stateInvulnerabilityLocks++;
    }

    private void EndStateInvulnerability()
    {
        if (stateInvulnerabilityLocks <= 0) return;
        stateInvulnerabilityLocks--;
        if (health != null) health.EndInvulnerability();
    }

    /// <summary>
    /// 이 컨트롤러가 건 무적 잠금을 전부 푼다. 상태가 겹쳐 잠기더라도 영구 무적으로 남지 않게,
    /// 노출 진입과 사망·비활성화에서 반드시 여기를 지난다.
    /// </summary>
    private void EndAllStateInvulnerability()
    {
        while (stateInvulnerabilityLocks > 0) EndStateInvulnerability();
    }

    private void ClearAttackObjects()
    {
        attackGeneration++;
        ClearFloods();
        if (attackRoot == null) return;

        for (int i = attackRoot.childCount - 1; i >= 0; i--)
            Destroy(attackRoot.GetChild(i).gameObject);
    }

    /// <summary>
    /// 외곽 바다의 잠항 표시와 하이드로펌프 원점이 화면에 들어오도록 시야를 넓힌다.
    ///
    /// URP의 <c>PixelPerfectCamera</c>는 참조 해상도에서 <c>orthographicSize</c>를 매 프레임 다시
    /// 계산해 여기서 넣은 값을 덮어쓰는데, 참조 해상도는 읽기 전용이라 바꿀 수 없다. 그래서 이
    /// 전투 동안만 그 컴포넌트를 끄고 방을 나갈 때 원래대로 되돌린다. 픽셀 스냅을 유지하는 쪽이
    /// 더 중요하면 <see cref="arenaCameraSize"/>를 0으로 두면 된다.
    /// </summary>
    private void ApplyArenaCamera()
    {
        if (arenaCameraSize <= 0f) return;
        arenaCamera = Camera.main;
        if (arenaCamera == null || !arenaCamera.orthographic) return;

        foreach (Behaviour behaviour in arenaCamera.GetComponents<Behaviour>())
        {
            if (behaviour == null || !behaviour.enabled) continue;
            if (behaviour.GetType().Name != "PixelPerfectCamera") continue;
            pixelPerfectCamera = behaviour;
            behaviour.enabled = false;
            break;
        }

        cameraSizeBeforeFight = arenaCamera.orthographicSize;
        arenaCamera.orthographicSize = arenaCameraSize;
    }

    private void RestoreCamera()
    {
        if (pixelPerfectCamera != null)
        {
            pixelPerfectCamera.enabled = true;
            pixelPerfectCamera = null;
        }
        if (arenaCamera == null || cameraSizeBeforeFight <= 0f) return;
        arenaCamera.orthographicSize = cameraSizeBeforeFight;
        cameraSizeBeforeFight = 0f;
        arenaCamera = null;
    }

    private void FaceTowardPlayer()
    {
        if (enemyAnimator == null) return;
        Vector2 delta = PlayerPosition - (Vector2)transform.position;
        enemyAnimator.SetActionState("Idle", delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down);
    }

    private void HandleCurrentTelegraph() =>
        Trace("해류 변경 예고 " + currentField.PreviousSignText + " → " + currentField.PendingSignText);

    private void HandleCurrentChanged() => Trace("해류 확정 " + currentField.SignText());

    /// <summary>수로 기준점이 계산된 수로 중앙과 크게 어긋나면 알려 준다. 방을 꾸밀 때만 쓴다.</summary>
    private void WarnAboutLaneAnchors()
    {
        if (!logPatterns) return;
        float third = ArenaHalfSize.y * 2f / 3f;
        CheckLaneAnchor(currentLaneBottom, ArenaCenter.y - third, "CurrentLane_Bottom");
        CheckLaneAnchor(currentLaneMiddle, ArenaCenter.y, "CurrentLane_Middle");
        CheckLaneAnchor(currentLaneTop, ArenaCenter.y + third, "CurrentLane_Top");
    }

    private void CheckLaneAnchor(Transform anchor, float expectedY, string label)
    {
        if (anchor == null)
        {
            Trace(label + " 기준점이 비어 있다");
            return;
        }
        if (Mathf.Abs(anchor.position.y - expectedY) > 0.5f)
            Trace(string.Format("{0}의 Y({1:0.00})가 계산된 수로 중앙({2:0.00})과 어긋난다",
                label, anchor.position.y, expectedY));
    }

    private void Trace(string message)
    {
        if (logPatterns) Debug.Log("[갸라도스] " + message, this);
    }

    /// <summary>전투 영역과 세 수로를 씬 뷰에 그린다. 기준점을 배치할 때 눈으로 맞춘다.</summary>
    private void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? ArenaCenter
            : (arenaBounds != null ? (Vector2)arenaBounds.position
                                   : (transform.parent != null ? (Vector2)transform.parent.position
                                                               : (Vector2)transform.position));
        Vector2 half = arenaBounds != null
            ? new Vector2(Mathf.Abs(arenaBounds.localScale.x) * 0.5f, Mathf.Abs(arenaBounds.localScale.y) * 0.5f)
            : arenaHalfSize;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(center, new Vector3(half.x * 2f, half.y * 2f, 0f));

        Gizmos.color = new Color(0.9f, 0.95f, 1f, 0.5f);
        for (int i = 0; i < 2; i++)
        {
            float y = center.y + (i == 0 ? -half.y / 3f : half.y / 3f);
            Gizmos.DrawLine(new Vector3(center.x - half.x, y, 0f), new Vector3(center.x + half.x, y, 0f));
        }

        // 반사 경계는 벽의 안쪽 면과 눈으로 맞춰야 한다. 전투 영역과 따로 그린다.
        if (hydroReflectBounds == null) return;
        Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(hydroReflectBounds.position,
            new Vector3(Mathf.Abs(hydroReflectBounds.localScale.x),
                        Mathf.Abs(hydroReflectBounds.localScale.y), 0f));
    }
}

/// <summary>
/// 갸라도스 전용 도형 스프라이트. <see cref="PrimitiveSprites"/>의 <c>Ring</c>은 테두리 두께가
/// 고정이라 "안쪽 반지름과 바깥 반지름 사이만 위험한" 똬리치기 고리를 그대로 그릴 수 없다.
/// 비율마다 하나씩 만들어 재사용한다.
/// </summary>
public static class GyaradosShapes
{
    private const int Resolution = 128;

    private static readonly Dictionary<int, Sprite> annuli = new Dictionary<int, Sprite>();

    /// <summary>
    /// 지름 1유닛의 고리. <paramref name="innerRatio"/> 안쪽은 비어 있다.
    /// localScale에 바깥 지름을 주면 실제 판정과 같은 크기가 된다.
    /// </summary>
    public static Sprite Annulus(float innerRatio)
    {
        // 0.01 단위로 캐시한다. 페이즈당 한 종류씩만 쓰므로 캐시가 커질 일은 없다.
        // 안쪽 비율이 1에 닿으면 아무것도 그려지지 않으므로 0.99에서 붙든다.
        int key = float.IsNaN(innerRatio) ? 0 : Mathf.Clamp(Mathf.RoundToInt(innerRatio * 100f), 0, 99);
        if (annuli.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Sprite sprite = Make(key / 100f);
        annuli[key] = sprite;
        return sprite;
    }

    private static Sprite Make(float innerRatio)
    {
        Texture2D tex = new Texture2D(Resolution, Resolution) { filterMode = FilterMode.Bilinear };
        float radius = Resolution * 0.5f;
        float inner = radius * innerRatio;
        Color[] pixels = new Color[Resolution * Resolution];

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                // 안팎 가장자리 1픽셀씩을 부드럽게 깎아 계단을 줄인다.
                float alpha = Mathf.Min(Mathf.Clamp01(radius - distance), Mathf.Clamp01(distance - inner));
                pixels[y * Resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Resolution, Resolution),
                                      new Vector2(0.5f, 0.5f), Resolution);
        sprite.name = "Annulus" + Mathf.RoundToInt(innerRatio * 100f);
        return sprite;
    }
}
