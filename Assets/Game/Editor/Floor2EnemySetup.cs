using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2층 적의 프리팹을 만들고 전투방에 배치하는 설정 스크립트. 수치의 원본이 여기다 —
/// 값을 고칠 일이 있으면 여기서 고치고 <see cref="ApplyRework"/>를 재실행한다.
///
/// 2층 컨셉은 <b>사막의 맹공 — 공격을 읽고 빈틈을 노리는 층</b>이다.
/// 접촉 피해는 전원 0. 모든 피해는 준비 동작이 보이는 기술의 타격 프레임에만 있고,
/// 강한 공격 뒤에는 반드시 일정 시간 움직임과 공격을 멈춘다. 적의 몸이 아니라
/// 공격 모션을 보고 피하고, 다음에 멈출 적을 예상해 공격 대상을 고르는 층이다.
///
/// <see cref="BuildPrefabs"/>는 스라크를 본으로 처음부터 새로 뜨는 것이라, 이미 그림자
/// 짝(PmdFootShadow)이 맺어진 프리팹에 다시 돌리면 그 짝이 본(스라크) 것으로 덮인다.
/// 살아 있는 프리팹에는 <see cref="ApplyRework"/>로 수치만 덧씌울 것.
/// </summary>
public static class Floor2EnemySetup
{
    private class EnemySpec
    {
        public string name;             // Enemy_{name}.prefab, Characters/{name}/
        public int health;
        public float scale;
        public float moveSpeed;
        public int goldMin;
        public int goldMax;
        public float keepDistance;
        public float knockbackMultiplier = 1f;
        public bool basicAI = true;
        public Vector2 boxSize = new Vector2(0.7f, 0.6f);
        public Type ability;
        // (필드 이름, 값) — 능력의 SerializedObject에 그대로 넣는다.
        public (string, object)[] abilityValues = Array.Empty<(string, object)>();
    }

    private static readonly EnemySpec[] Specs =
    {
        // 성원숭 — 2층의 기본 근접. 예고를 보이며 최대 3연속으로 짧게 돌진한다.
        // 맞히면 거기서 끝(0.7초), 세 번 다 빗나가면 지쳐서 길게 멈춘다(1.6초).
        new EnemySpec
        {
            name = "Primeape", health = 150, scale = 1.2f, moveSpeed = 4.4f,
            goldMin = 14, goldMax = 19,
            ability = typeof(EnemyComboMeleeAbility),
            abilityValues = new (string, object)[]
            {
                // 발동은 중심 거리 기준(EnemyAbility) — 돌진으로 거리를 좁히므로
                // 조금 멀리서도 시작할 수 있게 넉넉히 둔다.
                ("range", 5.5f), ("cooldown", 0.85f), ("initialDelay", 1.0f),
                ("actionState", "MultiStrike"), ("readyState", "Ready"),
                ("maxDashes", 3), ("windup", 0.32f), ("telegraph", 0.32f),
                ("dashDistance", 2.2f), ("dashSpeed", 9f), ("hitDelay", 0.18f),
                // 피격 무적(0.5초)보다 짧다 — 연타가 다 들어가지는 않지만 쉴 틈도 주지 않는다.
                ("betweenDashes", 0.38f),
                ("reach", 1.2f), ("sweepAngle", 150f), ("damage", 15),
                ("hitPause", 0.5f), ("missPause", 1f),
                // 예고 색을 붉은색으로 통일했다 — 회색 경로와 붉은 피해 범위가 섞여 읽히지 않았다.
                ("pathColor", new Color(0.88f, 0.12f, 0.2f, 0.24f)),
                ("hitColor", new Color(0.88f, 0.12f, 0.2f, 0.52f)),
            },
        },
        // 고지 — 자리를 두 번 묻는 적. 몸을 말고 짧게 굴러 붙은 뒤, 멈춘 자리에서
        // 사방으로 가시를 뿌린다. 돌진은 선을 보고 비키고, 가시는 갈래 사이에 선다.
        // 굴러온 자리가 곧 가시의 중심이라 첫 회피가 두 번째 회피를 정한다.
        new EnemySpec
        {
            // 이속은 3.0(처음)과 3.6(상향) 사이. 붙는 맛은 살리되 플레이어(5)를 따라붙지는 못한다.
            name = "Sandslash", health = 190, scale = 1.25f, moveSpeed = 3.9f,
            goldMin = 15, goldMax = 20, knockbackMultiplier = 0.6f,
            ability = typeof(EnemyRollSpikeAbility),
            abilityValues = new (string, object)[]
            {
                // 구르기로 거리를 좁히므로 붙기 전부터 시작할 수 있게 넉넉히 둔다.
                ("range", 6f), ("minRange", 0f), ("cooldown", 2.6f), ("initialDelay", 1f),
                ("readyState", "StrikeReady"), ("rollState", "Attack"),
                ("curlState", "Guard"), ("uncurlState", "Uncurl"),
                // 스라크 돌진(5.5)의 절반쯤. 붙는 수단이지 가로지르는 거리가 아니다.
                ("windup", 0.5f), ("dashDistance", 2.8f), ("dashSpeed", 10f),
                ("dashDamage", 12), ("dashHitRadius", 0.6f),
                // 열 갈래면 거리 3에서 갈래 사이가 1.9칸 — 플레이어(폭 0.7)가 설 만하다.
                ("spikeWindup", 0.55f), ("spikeCount", 10), ("spikeSpeed", 8f),
                ("spikeDamage", 12), ("spikeRadius", 0.17f), ("spikeLifetime", 1.6f),
                ("spikeTelegraphLength", 5f),
                // 몸 펴기(0.14) + 정지(0.9) ≈ 1초 — 다 뿌리고 나면 치르는 값이다.
                ("uncurlDuration", 0.14f), ("recovery", 0.9f),
            },
        },
        // 텅구리 — 중거리 견제. 거리를 지키며 왕복하는 뼈를 던지고, 뼈가 돌아올 때까지
        // 투척 자세로 무방비다. 맞혔으면 0.7초, 왕복이 다 빗나가면 1.3초 정지.
        new EnemySpec
        {
            name = "Marowak", health = 135, scale = 1.2f, moveSpeed = 3.6f,
            goldMin = 15, goldMax = 20, keepDistance = 3.2f,
            ability = typeof(EnemyBoomerangAbility),
            abilityValues = new (string, object)[]
            {
                ("range", 6.5f), ("minRange", 1.2f), ("cooldown", 0.95f), ("initialDelay", 1f),
                // 던지는 손이 빨라졌다 — 예고를 짧게, 뼈를 빠르게, 쿨을 절반으로.
                ("windup", 0.25f), ("boneSpeed", 14.5f), ("damage", 19),
                ("hitRecovery", 0.55f), ("missRecovery", 0.95f),
            },
        },
        // 닥트리오 — 이 층의 엘리트. 방마다 한 마리만 나오고, 체력·피해가 잡몹의 갑절이다.
        // 잠복 기습 한 방이 크고 넓은 대신 공격 후 땅 위에서 1.5초 스스로 스턴.
        // 여러 마리여도 한 번에 한 마리만 파고든다 (EnemyBurrowAbility.activeDiver).
        new EnemySpec
        {
            // 넉백 배율 1.6 — 몸통박치기(힘 6)에 맞으면 9.6으로 밀려난다. 파고들 때의 속도(9.5)와
            // 같게 맞춘 값이다. 맞고 땅속으로 밀려나는 것도 이 적에게는 '이동'이라 느리면 답답하다.
            name = "Dugtrio", health = 240, scale = 1.35f, moveSpeed = 2.5f,
            goldMin = 34, goldMax = 44, knockbackMultiplier = 1.1f, basicAI = false,
            boxSize = new Vector2(0.9f, 0.55f),
            ability = typeof(EnemyBurrowAbility),
            abilityValues = new (string, object)[]
            {
                // 사거리 = 방 전체. 잠수가 유일한 이동 수단이라, 사거리 밖이면 조각상이 되어
                // 어그로가 풀린 것처럼 보인다.
                // 엘리트답게 한 방이 무겁다. 대신 주기를 늘려 "가끔 오는 큰 것"으로 만든다.
                ("range", 20f), ("cooldown", 2.3f), ("initialDelay", 1.8f),
                ("surfaceWindup", 0.7f), ("surfaceRadius", 1.9f), ("damage", 36),
                ("recovery", 1.15f),
                // 맞고 나서 땅속으로 사라지는 것이 유일한 도피다. 플레이어(5)보다 확실히 빨라야
                // 쫓아가 잡는 게 아니라 놓치는 느낌이 된다.
                ("diveSpeed", 10.5f),
            },
        },
        // 나인테일 — 후열. 중거리를 지키다 부채꼴 예고와 함께 넓게 화염을 뿜는다.
        // 잔류 장판 없음, 직접 피해만. 분사가 끝나면 과열로 2초 정지.
        new EnemySpec
        {
            name = "Ninetales", health = 125, scale = 1.25f, moveSpeed = 4f,
            goldMin = 16, goldMax = 21, keepDistance = 4.2f,
            ability = typeof(EnemyFlameConeAbility),
            abilityValues = new (string, object)[]
            {
                ("range", 8f), ("minRange", 1f), ("cooldown", 1.35f), ("initialDelay", 1.2f),
                // 옆으로 돌아 피할 수 있어야 한다 — 각도를 좁히고 조준 회전에 상한을 뒀다.
                ("windup", 0.75f), ("coneRange", 5.2f), ("coneAngle", 45f), ("turnSpeed", 25f),
                ("sprayDuration", 1f), ("damage", 14), ("tickInterval", 0.45f),
                ("overheatDuration", 1.3f),
            },
        },
        // 데구리 — 예비 전력. 이번 개편의 방 구성에서는 빠졌지만, 프리팹은 같은 규칙을
        // 따르게 유지한다 (접촉 피해 0, 구르는 동안에만 판정).
        new EnemySpec
        {
            name = "Graveler", health = 180, scale = 1.3f, moveSpeed = 3.2f,
            goldMin = 18, goldMax = 22, knockbackMultiplier = 0.3f,
            boxSize = new Vector2(0.75f, 0.7f),
            ability = typeof(EnemyDashAbility),
            abilityValues = new (string, object)[]
            {
                // minRange 0 — 코뿌리의 이판사판처럼 코앞에서도 구른다. 최소 거리를 두면
                // 한 번 붙은 뒤로는 영영 구르지 않고 평범한 근접몹이 돼 버린다.
                ("range", 7f), ("minRange", 0f), ("cooldown", 4f), ("initialDelay", 1.3f),
                ("windup", 0.8f), ("dashSpeed", 16f), ("dashDistance", 7.5f),
                ("damage", 20), ("recovery", 0.9f), ("hitRadius", 0.65f),
                ("windupState", "Charge"), ("dashState", "Roll"),
                // 구르기 스프라이트가 이미 "굴러가는 돌"이다. 스라크처럼 붉게 물들이면
                // 오히려 바위 같지 않아 보인다.
                ("dashTint", Color.white),
            },
        },
    };

    // 방 구성. 방 이름 → (프리팹 이름, 로컬 좌표). 방 안쪽은 대략 ±6 × ±4다.
    //
    // 마릿수를 줄이고 한 마리를 강하게 했다. 다섯이 한꺼번에 달려들면 누구의 예고인지
    // 읽을 수가 없어, 결국 몸으로 부딪히는 싸움이 된다 — 2층이 피하려던 바로 그것이다.
    // 셋 안팎이면 "지금 누가 멈출 차례인지"를 눈으로 좇을 수 있다.
    // 닥트리오는 엘리트라 방마다 한 마리를 넘지 않고, 뒤쪽 두 방에만 나온다.
    private static readonly Dictionary<string, (string enemy, Vector2 at)[]> Rooms =
        new Dictionary<string, (string, Vector2)[]>
    {
        // 1번방 — 근접 둘의 소개. 돌진(성원숭)과 단발 강타(고지)를 따로 배운다.
        ["F2Room1_Combat"] = new (string, Vector2)[]
        {
            ("Sandslash", new Vector2(2.2f, 0f)),
            ("Primeape", new Vector2(4.6f, 2.4f)),
            ("Primeape", new Vector2(4.6f, -2.4f)),
        },
        // 2번방 — 근접을 상대하는 동안 왕복 뼈다귀가 등 뒤를 노린다.
        ["F2Room2_Combat"] = new (string, Vector2)[]
        {
            ("Primeape", new Vector2(2.4f, 1.8f)),
            ("Sandslash", new Vector2(2.4f, -1.8f)),
            ("Marowak", new Vector2(5.4f, 0f)),
        },
        // 4번방 — 엘리트 첫 등장. 전위 뒤에서 화염이 길을 막고, 발밑에서 기습이 온다.
        ["F2Room4_Combat"] = new (string, Vector2)[]
        {
            ("Sandslash", new Vector2(2.2f, 1.8f)),
            ("Ninetales", new Vector2(5.4f, -1.6f)),
            ("Dugtrio", new Vector2(4f, 3.2f)),
        },
        // 5번방 — 혼합 전투. 멈출 적을 골라 때리는 층의 최종 시험.
        ["F2Room5_Combat"] = new (string, Vector2)[]
        {
            ("Primeape", new Vector2(2.4f, 2f)),
            ("Marowak", new Vector2(5.6f, -2.2f)),
            ("Ninetales", new Vector2(5.6f, 2.2f)),
            ("Dugtrio", new Vector2(3.4f, -3f)),
        },
    };

    private const string TemplatePath = "Assets/Game/Prefabs/Enemies/Enemy_Scyther.prefab";

    // ---------------------------------------------------------------- 개편 적용

    /// <summary>
    /// 살아 있는 프리팹에 개편 수치를 덧씌운다. 접촉 피해를 0으로 내리고, 능력이 스펙과
    /// 다른 형이면(나인테일: 화염 줄기 → 부채꼴) 기존 능력을 떼고 새로 단 뒤 값을 채운다.
    /// 프리팹을 새로 뜨지 않으므로 그림자 짝·스프라이트 참조가 그대로 산다.
    /// </summary>
    public static string ApplyRework()
    {
        var log = new System.Text.StringBuilder();
        foreach (EnemySpec spec in Specs)
        {
            string path = "Assets/Game/Prefabs/Enemies/Enemy_" + spec.name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            { log.AppendLine(spec.name + ": 프리팹 없음 — BuildPrimeape 먼저"); continue; }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // 지워진 스크립트(화염 줄기)가 빈 컴포넌트로 남아 있으면 걷어낸다.
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

                Set(root.GetComponent<Health>(), ("maxHealth", spec.health));
                Set(root.GetComponent<EnemyController>(),
                    ("moveSpeed", spec.moveSpeed),
                    ("attackDamage", 0),          // 접촉 피해 없음 — 기술 판정만 남는다
                    ("goldRewardMin", spec.goldMin),
                    ("goldRewardMax", spec.goldMax),
                    ("keepDistance", spec.keepDistance),
                    ("knockbackMultiplier", spec.knockbackMultiplier),
                    ("basicAIEnabled", spec.basicAI));

                bool swapped = false;
                if (root.GetComponent(spec.ability) == null)
                {
                    foreach (EnemyAbility old in root.GetComponents<EnemyAbility>())
                        UnityEngine.Object.DestroyImmediate(old);
                    root.AddComponent(spec.ability);
                    swapped = true;
                }
                Set(root.GetComponent(spec.ability) as Component, spec.abilityValues);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine(spec.name + ": 접촉 0, " + spec.ability.Name +
                               (swapped ? " (교체)" : " 갱신"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ---------------------------------------------------------------- 프리팹 뜨기

    /// <summary>
    /// 성원숭 프리팹을 처음 만든다. 그 뒤 그림자는 ShadowSetup.SliceOne("Primeape",
    /// "0057_Primeape") → AttachOne("Primeape") 순서로, 서로 다른 명령에서 붙일 것.
    /// </summary>
    public static string BuildPrimeape()
    {
        string result = BuildPrefab(Array.Find(Specs, s => s.name == "Primeape"));
        AssetDatabase.SaveAssets();
        return result;
    }

    /// <summary>
    /// ⚠️ 다섯 종을 전부 본에서 새로 뜬다. 그림자 짝이 본 것으로 덮이므로,
    /// 이미 배포된 프리팹에는 <see cref="ApplyRework"/>를 쓸 것.
    /// </summary>
    public static string BuildPrefabs()
    {
        var log = new System.Text.StringBuilder();
        foreach (EnemySpec spec in Specs)
            log.AppendLine(BuildPrefab(spec));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private static string BuildPrefab(EnemySpec spec)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TemplatePath);
        try
        {
            root.name = "Enemy_" + spec.name;
            root.transform.localScale = Vector3.one * spec.scale;

            // 겉모습 — 전용 컨트롤러를 먼저, 스프라이트를 나중에.
            // 순서가 반대면 플레이 모드에서 구울 때 스프라이트가 본(스라크) 것으로 되돌아간다:
            // 컨트롤러를 갈아 끼우는 순간 애니메이터가 다시 바인딩되며, 처음 바인딩 때
            // 기록해 둔 기본값(본의 스프라이트)을 SpriteRenderer에 도로 써 넣기 때문이다.
            string artRoot = "Assets/Game/Art/Characters/" + spec.name;
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(artRoot + "/" + spec.name + ".controller");
            Sprite idle = FindSprite(artRoot + "/Sprites/Walk.png", "Walk_0_0");
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = idle;

            var box = root.GetComponent<BoxCollider2D>();
            box.size = spec.boxSize;

            Set(root.GetComponent<Health>(), ("maxHealth", spec.health));
            Set(root.GetComponent<EnemyController>(),
                ("moveSpeed", spec.moveSpeed),
                ("attackDamage", 0),
                ("goldRewardMin", spec.goldMin),
                ("goldRewardMax", spec.goldMax),
                ("keepDistance", spec.keepDistance),
                ("knockbackMultiplier", spec.knockbackMultiplier),
                ("basicAIEnabled", spec.basicAI));

            // 본(스라크)에 붙어 있던 능력을 전부 떼고 전용 능력을 단다.
            // 스라크는 돌진(EnemyDashAbility)에 근접기(EnemyMeleeAbility)까지 들고 있다.
            foreach (EnemyAbility old in root.GetComponents<EnemyAbility>())
                UnityEngine.Object.DestroyImmediate(old);
            Component ability = root.AddComponent(spec.ability);
            Set(ability, spec.abilityValues);

            string path = "Assets/Game/Prefabs/Enemies/Enemy_" + spec.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return spec.name + ": 저장 (체력 " + spec.health + ", 능력 " + spec.ability.Name + ")";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 방 배치

    public static string PlaceInRooms()
    {
        var log = new System.Text.StringBuilder();
        foreach (var pair in Rooms)
        {
            string path = "Assets/Game/Prefabs/Rooms/" + pair.Key + ".prefab";
            GameObject room = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // 이전 배치를 전부 걷어낸다.
                int removed = 0;
                foreach (EnemyController old in room.GetComponentsInChildren<EnemyController>())
                {
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                    removed++;
                }

                foreach ((string enemy, Vector2 at) in pair.Value)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Game/Prefabs/Enemies/Enemy_" + enemy + ".prefab");
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room.transform);
                    instance.transform.localPosition = at;
                }

                PrefabUtility.SaveAsPrefabAsset(room, path);
                log.AppendLine(pair.Key + ": " + removed + "마리 제거, " + pair.Value.Length + "마리 배치");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(room);
            }
        }
        return log.ToString();
    }

    /// <summary>
    /// 2층 이벤트 방의 시라소몬·홍수몬 NPC를 세운다. 가운데 기준 왼쪽/오른쪽에 일반
    /// 포켓몬 크기로 서서, 남쪽(아래)을 보고 수련 동작을 반복한다. 제자로 선택되면
    /// <see cref="MartialArtsEvent"/>가 그 스승만 Idle로 바꾼다.
    ///
    /// ⚠️ 반드시 에디트 모드에서, 파이프라인 재실행과 다른 명령으로 실행할 것
    /// (컨트롤러 먼저·스프라이트 나중 규칙과 같은 이유 — progress.md 참고).
    /// </summary>
    public static string SetupMartialArtsNpcs()
    {
        string path = "Assets/Game/Prefabs/Rooms/F2Room3_Event.prefab";
        GameObject room = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var npcs = new (string name, string state, float x)[]
            {
                ("Hitmonlee", "Kick_0", -1.5f),
                ("Hitmonchan", "Punch_0", 1.5f),
            };

            // ⚠ 두 NPC의 부모 "Masters"가 (3, 2)로 비균일하게 늘어나 있었다 — 스프라이트가
            // 눌려 보인 진짜 원인. 자식 스케일을 아무리 1로 둬도 소용없으니 부모부터 편다.
            Transform holder = FindChildByName(room.transform, "Masters");
            if (holder != null)
            {
                holder.localPosition = Vector3.zero;
                holder.localScale = Vector3.one;
            }

            var poses = new Dictionary<string, EventNpcPose>();
            foreach ((string name, string state, float x) in npcs)
            {
                Transform npc = FindChildByName(room.transform, name);
                if (npc == null) return name + "을(를) 방에서 찾지 못했다";

                npc.localPosition = new Vector3(x, 0f, 0f);
                // 실측 기준: 두 스승의 몸이 25px, 플레이어(21px × 1.2배) = 25.2px — 1배가 곧
                // 플레이어와 같은 한 타일 크기다.
                npc.localScale = Vector3.one;

                // NPC는 몸으로 막는다. Rigidbody 없는 정적 콜라이더라 밀리지도 않는다.
                BoxCollider2D box = npc.GetComponent<BoxCollider2D>();
                if (box == null) box = npc.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.6f, 0.5f);
                box.offset = new Vector2(0f, -0.15f);   // 발치만 막아 머리 위로는 지나가 보인다

                string artRoot = "Assets/Game/Art/Characters/" + name;
                Animator animator = npc.GetComponent<Animator>();
                if (animator == null) animator = npc.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    artRoot + "/" + name + ".controller");

                // 기본 스프라이트는 수련 동작의 남쪽 첫 프레임. (컨트롤러를 먼저 할당했다.)
                string sheet = state.Substring(0, state.IndexOf('_'));
                var renderer = npc.GetComponent<SpriteRenderer>();
                renderer.sprite = FindSprite(artRoot + "/Sprites/" + sheet + ".png", sheet + "_0_0");

                EventNpcPose pose = npc.GetComponent<EventNpcPose>();
                if (pose == null) pose = npc.gameObject.AddComponent<EventNpcPose>();
                Set(pose, ("initialState", state));
                poses[name] = pose;
            }

            MartialArtsEvent martial = room.GetComponentInChildren<MartialArtsEvent>();
            if (martial == null) return "MartialArtsEvent를 찾지 못했다";
            var so = new SerializedObject(martial);
            so.FindProperty("hitmonleeNpc").objectReferenceValue = poses["Hitmonlee"];
            so.FindProperty("hitmonchanNpc").objectReferenceValue = poses["Hitmonchan"];
            so.ApplyModifiedPropertiesWithoutUndo();

            int drained = DrainPond(room);

            PrefabUtility.SaveAsPrefabAsset(room, path);
            return "이벤트 방 NPC 재배선 완료 (±1.5, 크기 1.0, Kick_0/Punch_0), 호수 타일 " + drained + "칸 제거";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    /// <summary>
    /// 방의 호수를 모래로 메운다. 물 타일(D_24~26_*)을 전부 바닥 타일(D_13_1)로 갈고
    /// 물에 빠지지 않게 막던 PondCollider도 지운다.
    /// </summary>
    private static int DrainPond(GameObject room)
    {
        int replaced = 0;
        foreach (var map in room.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>())
        {
            // 바닥 타일 에셋을 맵에서 직접 찾는다 (이름으로 에셋을 뒤지는 것보다 확실하다).
            UnityEngine.Tilemaps.TileBase sand = null;
            foreach (var pos in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(pos);
                if (tile != null && tile.name == "D_13_1") { sand = tile; break; }
            }
            if (sand == null) continue;

            foreach (var pos in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(pos);
                if (tile == null) continue;
                if (tile.name.StartsWith("D_24_") || tile.name.StartsWith("D_25_") ||
                    tile.name.StartsWith("D_26_"))
                {
                    map.SetTile(pos, sand);
                    replaced++;
                }
            }
        }

        Transform pond = FindChildByName(room.transform, "PondCollider");
        if (pond != null) UnityEngine.Object.DestroyImmediate(pond.gameObject);
        return replaced;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    // ---------------------------------------------------------------- 도구

    private static Sprite FindSprite(string sheetPath, string name)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        return null;
    }

    /// <summary>직렬화된 비공개 필드를 이름으로 채운다. 이름이 틀리면 소리 내고 실패한다.</summary>
    private static void Set(Component target, params (string field, object value)[] values)
    {
        var so = new SerializedObject(target);
        foreach ((string field, object value) in values)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
                throw new ArgumentException(target.GetType().Name + "에 " + field + " 필드가 없다");
            switch (value)
            {
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case bool b: prop.boolValue = b; break;
                case string s: prop.stringValue = s; break;
                case Color c: prop.colorValue = c; break;
                default: throw new ArgumentException(field + ": 지원하지 않는 형 " + value.GetType());
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
