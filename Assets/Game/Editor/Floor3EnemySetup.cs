using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3층 적 다섯 종의 프리팹을 만들고 전투방에 배치하는 일회성 설정 스크립트.
/// 수치의 원본이 여기다 — 프리팹을 다시 만들 일이 있으면 값을 여기서 고치고 재실행한다.
///
/// 3층의 컨셉은 CC 연계다: 킹크랩(밀치기)·강챙이(흡인)·쥬래곤(감속·빙결)·신뇽(해류)이
/// 플레이어의 위치를 흔들고, 아쿠스타(레이저)가 주요 피해를 담당한다. 그래서 CC 담당의
/// 피해는 낮고, 각 종의 첫 시전 시간(initialDelay)을 서로 어긋나게 둬서 해류·흡인·빙결이
/// 동시에 최대 강도로 겹치지 않게 한다. 접촉 피해는 2층과 같이 전원 0이다 — 모든 피해는
/// 예고가 보이는 기술의 타격 순간에만 있다.
///
/// <b>속도가 이 층의 난이도다.</b> 체력·피해는 2층과 비슷한데도 3층이 훨씬 쉬웠던 까닭은
/// 한 마리가 한 번 공격하는 데 6~10초가 걸렸기 때문이다 — 예고를 읽을 것도 없이 그냥
/// 걸어 다니면 아무 일도 일어나지 않았다. 그래서 체력을 2층 위로 올리고(잡몹 130~205,
/// 엘리트 310) 이동·예고·후딜·쿨다운을 전부 당겨, 한 바퀴를 1.4~1.5배 빠르게 만들었다.
/// CC의 세기(당기는 힘·미는 힘)도 함께 올렸지만 <b>전부 플레이어 이동 속도 5보다 느리다</b> —
/// 이 층의 CC는 갈 수 있는 곳을 막는 것이 아니라 가는 데 드는 시간을 늘리는 것이라는
/// 규칙은 그대로다.
///
/// 프리팹은 스라크(Enemy_Scyther)를 본으로 뜬다. 2층(<see cref="Floor2EnemySetup"/>)과 같은
/// 방식이며, 컨트롤러 먼저·스프라이트 나중 규칙도 같다. 반드시 에디트 모드에서,
/// 파이프라인 재실행과는 다른 명령으로 실행할 것.
/// </summary>
public static class Floor3EnemySetup
{
    private class EnemySpec
    {
        public string name;
        public int health;
        public float scale;
        public float moveSpeed;
        public int contactDamage;
        public int goldMin;
        public int goldMax;
        public float keepDistance;
        public float knockbackMultiplier = 1f;
        public Vector2 boxSize = new Vector2(0.7f, 0.6f);
        public Type ability;
        public (string, object)[] abilityValues = Array.Empty<(string, object)>();
    }

    private static readonly EnemySpec[] Specs =
    {
        // 킹크랩 — 밀치기 전위. 접근해서 부채꼴 가위치기로 플레이어를 밀어낸다.
        new EnemySpec
        {
            name = "Kingler", health = 180, scale = 1.25f, moveSpeed = 4.3f,
            contactDamage = 0, goldMin = 5, goldMax = 13, knockbackMultiplier = 0.7f,
            boxSize = new Vector2(0.85f, 0.6f),
            ability = typeof(EnemyPincerAbility),
            abilityValues = new (string, object)[]
            {
                // 시전 거리는 부채꼴 반지름(3.0)보다 조금 넓게 둔다. 같거나 좁으면 가장자리에
                // 걸린 플레이어에게는 아예 시전하지 않아, 늘린 사거리가 헛돈다.
                ("range", 3.2f), ("reach", 3f), ("cooldown", 2.6f), ("initialDelay", 1f),
                // 한 바퀴 1.75 + 4 = 5.75초에서 1.15 + 2.6 = 3.75초로. 2층 성원숭이 0.32초
                // 예고로 달려드는 층 다음인데, 0.55초 예고에 1초를 쉬면 무는 맛이 없었다.
                ("telegraph", 0.4f), ("hitDelay", 0.15f), ("recovery", 0.6f),
                // 8 → 16. 밀리기만 하고 아프지 않으면 그냥 붙어 있는 쪽이 이득이 된다.
                ("damage", 16),
            },
        },
        // 강챙이 — 흡인형 근접. 소용돌이로 당겼다가 충격파로 되민다.
        new EnemySpec
        {
            name = "Poliwrath", health = 205, scale = 1.25f, moveSpeed = 3.6f,
            contactDamage = 0, goldMin = 5, goldMax = 15, knockbackMultiplier = 0.7f,
            ability = typeof(EnemyVortexAbility),
            abilityValues = new (string, object)[]
            {
                // 흡인 반지름(4.0)보다 약간 넓을 때부터 시작해, 걸어오는 플레이어를 마중한다.
                // 흡인과 충격파는 2:1 비율을 지킨다 — 충격파가 흡인에 비해 커지면
                // "당겨지는 동안 걸어 나가면 산다"는 규칙이 성립하지 않는다.
                // 첫 시전 1.6초 — 쥬래곤의 첫 냉기(1.2초)와 겹치지 않게 어긋내는 값.
                ("range", 4.8f), ("vortexRadius", 4f), ("blastRadius", 2f),
                ("cooldown", 4f), ("initialDelay", 1.6f),
                // 당기는 힘 2.7 → 3.4. 여전히 플레이어(5)보다 느려 거슬러 걸어 나갈 수 있지만,
                // 예전에는 너무 느려서 "빠져나갈 수 있다"가 아니라 "그냥 안 걸린다"였다.
                ("telegraph", 0.45f), ("pullDuration", 1.2f), ("pullSpeed", 3.4f),
                ("recovery", 0.9f),
            },
        },
        // 쥬래곤 — 감속 전담 후열. 방 저편에서 가느다란 냉기를 겨누고 계속 따라 돌린다.
        // 빙결은 뺐다. 몸이 통째로 멈추는 것은 피해가 없는 기술이 치를 값이 아니다.
        // 대신 조준이 쫓아온다 — 이 적이 하는 일은 느리게 만드는 것 하나이고, 그 하나는
        // 거의 확실히 해낸다. 느려진 채로 다른 적의 예고를 읽는 것이 3층의 숙제다.
        new EnemySpec
        {
            // 이동 3.9 → 2.8. 방 끝에서 겨누는 것이 일인 적이 발까지 빠를 이유가 없다.
            // 대기 거리 2.6 → 6.5 — 회전 상한이 만드는 '피할 수 없는 경계'(약 6.4칸)
            // 바로 바깥이다. 여기까지 걸어가 붙는 것이 이 적의 답이다.
            name = "Dewgong", health = 145, scale = 1.25f, moveSpeed = 2.8f,
            contactDamage = 0, goldMin = 4, goldMax = 12, keepDistance = 6.5f,
            ability = typeof(EnemyFrostBreathAbility),
            abilityValues = new (string, object)[]
            {
                // 시야 끝에서 알아본다: 시전 거리 4.4 → 10.5, 부채꼴 반지름 5.5 → 11.5.
                // 반지름이 시전 거리보다 넓은 관계는 그대로다 — 가장자리에서 시작한 분사가
                // 뒤로 달아나는 플레이어를 놓치면 감속 역할이 헛돈다.
                ("range", 10.5f), ("reach", 11.5f), ("minRange", 0.8f),
                ("cooldown", 2.8f), ("initialDelay", 1.2f),
                // ⚠️ 사거리를 두 배로 늘렸으면 각도는 반드시 좁혀야 한다. 70°를 그대로 두면
                // 반지름 11.5의 부채꼴이 88유닛², 방(14×10)의 63%를 덮는다. 22°면 25유닛²로
                // 예전(18.5)보다 조금 넓은 정도에 머문다.
                ("sweepAngle", 22f),
                // 회전 상한이 곧 거리별 난이도다. 옆으로 도는 플레이어의 각속도는 5÷거리이므로
                // 45°/초와 같아지는 6.4칸 밖에서는 아무리 돌아도 각이 벌어지지 않는다.
                ("turnSpeed", 45f),
                ("telegraph", 0.45f), ("breathDuration", 1.5f),
                ("maxSlowExposure", 0.8f), ("recovery", 0.7f),
            },
        },
        // 아쿠스타 — 기하학형 원거리 딜러. 외곽으로 순간이동해 +/× 레이저를 쏜다.
        new EnemySpec
        {
            name = "Starmie", health = 130, scale = 1.2f, moveSpeed = 4f,
            contactDamage = 0, goldMin = 3, goldMax = 11, keepDistance = 3.8f,
            ability = typeof(EnemyStarLaserAbility),
            abilityValues = new (string, object)[]
            {
                // 사거리 = 방 전체. 어디서든 외곽으로 이동해 쏘는 것이 패턴이다.
                ("range", 20f), ("minRange", 0f), ("cooldown", 2.8f), ("initialDelay", 1f),
                // 예고 0.75 → 0.55초. 갈래가 넷뿐이고 전부 직선이라 이 정도로도 읽힌다.
                // 켜져 있는 시간은 0.35 → 0.9초로 크게 늘렸다. 이 층의 CC는 플레이어를
                // 옮기기만 하므로 옮겨진 자리가 위험해야 뜻이 생기는데, 짧은 빔은 밀려나는
                // 동안 이미 꺼져 있어 밀치기·흡인·해류가 전부 헛돌았다. 0.9초면 피격
                // 무적(0.5초)이 한 번 돌아, 머물면 두 번까지 맞는다.
                ("teleportTelegraph", 0.32f), ("laserTelegraph", 0.55f),
                ("laserDuration", 0.9f), ("recovery", 0.7f),
            },
        },
        // 신뇽 — 해류 지원 엘리트. 마지막 일반 전투방에 한 마리만 나온다.
        // 아쿠스타와 같은 원거리 적이다. 붙어서 몸으로 싸우는 적이 아니라 해류를 깔고
        // 물러나 있어야 하므로, 아쿠스타(3.8)보다 조금 더 떨어진 거리를 유지한다.
        new EnemySpec
        {
            name = "Dragonair", health = 310, scale = 1.3f, moveSpeed = 3.7f,
            contactDamage = 0, goldMin = 9, goldMax = 23, knockbackMultiplier = 0.5f,
            keepDistance = 4.2f,
            boxSize = new Vector2(0.7f, 0.75f),
            ability = typeof(EnemyCurrentBandAbility),
            abilityValues = new (string, object)[]
            {
                // 띠 지속(4초)이 쿨다운보다 길어서, 주기를 정하는 것은 쿨다운이 아니라
                // "앞 띠가 걷힐 때까지 기다린다"는 규칙이다 — 약 5초마다 방향이 바뀐다.
                // 첫 시전 2.2초 — 방에 들어서자마자 해류부터 깔리면 다른 CC를 배울 틈이 없다.
                ("range", 20f), ("minRange", 0f), ("cooldown", 2.2f), ("initialDelay", 2.2f),
                // 미는 힘 2.4 → 3.2. 플레이어(5)보다는 여전히 느려 거스를 수 있지만,
                // 해류를 무시하고 걷던 것이 이제는 "가는 데 드는 시간"으로 돌아온다.
                ("pushSpeed", 3.2f), ("telegraph", 0.55f),
            },
        },
    };

    // 방 구성. 방 이름 → (프리팹 이름, 로컬 좌표). 방 안쪽은 대략 ±6 × ±4다.
    //
    // 2 → 4 → 5 → 6마리로 늘린다. 예전에는 4 → 5 → 7 → 6이라 4번방이 층에서 가장 붐볐는데,
    // 정작 엘리트가 나오는 마지막 방보다 앞이라 봉우리가 두 번 왔다.
    //
    // <b>쥬래곤은 방마다 한 마리씩만.</b> 사거리 10.5의 조준이 쫓아오는 적이라 둘이 서로 다른
    // 방향에서 겨누면 22° 두 줄이 교차해 설 자리가 없어진다. 한 마리면 "느려진 채로 다른 적을
    // 상대한다"이고, 둘이면 "느려진 채로 가만히 있는다"가 된다.
    //
    // 아쿠스타는 2번방부터 항상 둘이다. 이 층에서 직접 피해를 내는 것이 사실상 이 적뿐이라,
    // 나머지가 자리를 흔드는 동안 실제로 체력을 깎는 쪽이 꾸준히 있어야 한다.
    private static readonly Dictionary<string, (string enemy, Vector2 at)[]> Rooms =
        new Dictionary<string, (string, Vector2)[]>
    {
        // 1번방 — 흡인과 감속만. 당겨지는 몸과 느려지는 발을 하나씩 따로 배운다.
        ["F3Room1_Combat"] = new (string, Vector2)[]
        {
            ("Poliwrath", new Vector2(2.8f, -1.2f)),
            ("Dewgong", new Vector2(5.8f, 1.8f)),
        },
        // 2번방 — 아쿠스타 합류. 당겨지고 느려진 자리로 레이저가 지나간다.
        ["F3Room2_Combat"] = new (string, Vector2)[]
        {
            ("Poliwrath", new Vector2(2.8f, 0f)),
            ("Dewgong", new Vector2(5.9f, -2.4f)),
            ("Starmie", new Vector2(5.4f, 2.6f)),
            ("Starmie", new Vector2(3.4f, 3.4f)),
        },
        // 4번방 — 흡인이 빠지고 킹크랩이 들어온다. 당기는 대신 밀어내는 방이다.
        // 미는 방향과 레이저가 지나갈 자리를 함께 읽는 것이 여기서 배울 것이다.
        ["F3Room4_Combat"] = new (string, Vector2)[]
        {
            ("Kingler", new Vector2(1.8f, 1.8f)),
            ("Kingler", new Vector2(1.8f, -1.8f)),
            ("Dewgong", new Vector2(5.9f, 0f)),
            ("Starmie", new Vector2(5.2f, 3f)),
            ("Starmie", new Vector2(5.2f, -3f)),
        },
        // 5번방 — 엘리트 종합. 자리를 옮기는 것 셋(해류·흡인 둘)과 그 자리를 태우는 것
        // 둘만 남겼다. 쥬래곤을 뺀 것은 겹치는 성질이라서다 — 해류와 흡인이 이미 발을
        // 묶는데 감속까지 얹으면 세 겹이 같은 일을 하고, 정작 읽을 것은 하나도 늘지 않는다.
        ["F3Room5_Combat"] = new (string, Vector2)[]
        {
            ("Dragonair", new Vector2(4.4f, 0f)),
            ("Poliwrath", new Vector2(2.6f, 2.4f)),
            ("Poliwrath", new Vector2(2.6f, -2.4f)),
            ("Starmie", new Vector2(5.6f, 2.4f)),
            ("Starmie", new Vector2(5.6f, -2.4f)),
        },
    };

    private const string TemplatePath = "Assets/Game/Prefabs/Enemies/Enemy_Scyther.prefab";

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

            // 컨트롤러를 먼저, 스프라이트를 나중에 — 순서가 반대면 리바인드가 본(스라크)의
            // 스프라이트를 도로 써 넣는다 (progress.md의 플레이 모드 함정 참고).
            string artRoot = "Assets/Game/Art/Characters/" + spec.name;
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(artRoot + "/" + spec.name + ".controller");
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = FindSprite(artRoot + "/Sprites/Walk.png", "Walk_0_0");

            root.GetComponent<BoxCollider2D>().size = spec.boxSize;

            Set(root.GetComponent<Health>(), ("maxHealth", spec.health));
            Set(root.GetComponent<EnemyController>(),
                ("moveSpeed", spec.moveSpeed),
                ("attackDamage", spec.contactDamage),
                ("goldRewardMin", spec.goldMin),
                ("goldRewardMax", spec.goldMax),
                ("keepDistance", spec.keepDistance),
                ("knockbackMultiplier", spec.knockbackMultiplier),
                ("basicAIEnabled", true));

            var oldAbility = root.GetComponent<EnemyDashAbility>();
            if (oldAbility != null) UnityEngine.Object.DestroyImmediate(oldAbility);
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

    public static string PlaceInRooms()
    {
        var log = new System.Text.StringBuilder();
        foreach (var pair in Rooms)
        {
            string path = "Assets/Game/Prefabs/Rooms/" + pair.Key + ".prefab";
            GameObject room = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // 기존(임시) 적을 전부 걷어내고 새 구성으로 채운다. 지형·장식은 그대로다.
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

    private static Sprite FindSprite(string sheetPath, string name)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        return null;
    }

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
