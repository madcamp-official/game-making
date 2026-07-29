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
/// 동시에 최대 강도로 겹치지 않게 한다.
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
        public int gold;
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
            name = "Kingler", health = 130, scale = 1.25f, moveSpeed = 3.6f,
            contactDamage = 10, gold = 14, knockbackMultiplier = 0.7f,
            boxSize = new Vector2(0.85f, 0.6f),
            ability = typeof(EnemyPincerAbility),
            abilityValues = new (string, object)[]
            {
                // 시전 거리는 부채꼴 반지름(3.0)보다 조금 넓게 둔다. 같거나 좁으면 가장자리에
                // 걸린 플레이어에게는 아예 시전하지 않아, 늘린 사거리가 헛돈다.
                ("range", 3.2f), ("reach", 3f), ("cooldown", 4f), ("initialDelay", 1f),
            },
        },
        // 강챙이 — 흡인형 근접. 소용돌이로 당겼다가 충격파로 되민다.
        new EnemySpec
        {
            name = "Poliwrath", health = 150, scale = 1.25f, moveSpeed = 2.9f,
            contactDamage = 10, gold = 16, knockbackMultiplier = 0.7f,
            ability = typeof(EnemyVortexAbility),
            abilityValues = new (string, object)[]
            {
                // 흡인 반지름(4.0)보다 약간 넓을 때부터 시작해, 걸어오는 플레이어를 마중한다.
                // 흡인과 충격파는 2:1 비율을 지킨다 — 충격파가 흡인에 비해 커지면
                // "당겨지는 동안 걸어 나가면 산다"는 규칙이 성립하지 않는다.
                // 첫 시전 2.2초 — 쥬래곤의 첫 냉기(1.6초)와 겹치지 않게 어긋내는 값.
                ("range", 4.8f), ("vortexRadius", 4f), ("blastRadius", 2f),
                ("cooldown", 6f), ("initialDelay", 2.2f),
            },
        },
        // 쥬래곤 — 감속 지원. 냉기 부채꼴로 늦추고, 오래 노출되면 잠깐 얼린다.
        new EnemySpec
        {
            name = "Dewgong", health = 100, scale = 1.25f, moveSpeed = 3.2f,
            contactDamage = 8, gold = 14, keepDistance = 2.6f,
            ability = typeof(EnemyFrostBreathAbility),
            abilityValues = new (string, object)[]
            {
                // 부채꼴 반지름(5.5)이 시전 거리(4.4)보다 넓다. 일부러 그렇게 뒀다 —
                // 예고를 보고 뒤로 달아나는 플레이어까지 냉기가 따라붙어야 감속 역할이 산다.
                ("range", 4.4f), ("reach", 5.5f), ("minRange", 0.8f),
                ("cooldown", 4.5f), ("initialDelay", 1.6f),
            },
        },
        // 아쿠스타 — 기하학형 원거리 딜러. 외곽으로 순간이동해 +/× 레이저를 쏜다.
        new EnemySpec
        {
            name = "Starmie", health = 90, scale = 1.2f, moveSpeed = 3.4f,
            contactDamage = 8, gold = 16, keepDistance = 3.8f,
            ability = typeof(EnemyStarLaserAbility),
            abilityValues = new (string, object)[]
            {
                // 사거리 = 방 전체. 어디서든 외곽으로 이동해 쏘는 것이 패턴이다.
                ("range", 20f), ("minRange", 0f), ("cooldown", 4.5f), ("initialDelay", 1.4f),
            },
        },
        // 신뇽 — 해류 지원 엘리트. 마지막 일반 전투방에 한 마리만 나온다.
        // 아쿠스타와 같은 원거리 적이다. 붙어서 몸으로 싸우는 적이 아니라 해류를 깔고
        // 물러나 있어야 하므로, 아쿠스타(3.8)보다 조금 더 떨어진 거리를 유지한다.
        new EnemySpec
        {
            name = "Dragonair", health = 220, scale = 1.3f, moveSpeed = 3.2f,
            contactDamage = 12, gold = 26, knockbackMultiplier = 0.5f,
            keepDistance = 4.2f,
            boxSize = new Vector2(0.7f, 0.75f),
            ability = typeof(EnemyCurrentBandAbility),
            abilityValues = new (string, object)[]
            {
                // 띠 지속(4초) + 쿨다운 3초 → 약 7초 주기로 자리·방향이 바뀐다.
                // 첫 시전 3초 — 방에 들어서자마자 해류부터 깔리면 다른 CC를 배울 틈이 없다.
                ("range", 20f), ("minRange", 0f), ("cooldown", 3f), ("initialDelay", 3f),
            },
        },
    };

    // 방 구성. 방 이름 → (프리팹 이름, 로컬 좌표). 방 안쪽은 대략 ±6 × ±4다.
    private static readonly Dictionary<string, (string enemy, Vector2 at)[]> Rooms =
        new Dictionary<string, (string, Vector2)[]>
    {
        // 1번방 — 밀치기와 감속 소개. 밀리는 방향과 느려지는 발을 따로따로 배운다.
        ["F3Room1_Combat"] = new (string, Vector2)[]
        {
            ("Kingler", new Vector2(2f, 1.5f)),
            ("Kingler", new Vector2(2f, -1.5f)),
            ("Dewgong", new Vector2(4.5f, 2.5f)),
            ("Dewgong", new Vector2(4.5f, -2.5f)),
        },
        // 2번방 — 아쿠스타 합류. 밀치기·감속에 밀린 자리로 레이저가 지나간다.
        ["F3Room2_Combat"] = new (string, Vector2)[]
        {
            ("Kingler", new Vector2(1.8f, 1.8f)),
            ("Kingler", new Vector2(1.8f, -1.8f)),
            ("Dewgong", new Vector2(4.2f, 2.6f)),
            ("Dewgong", new Vector2(4.2f, -2.6f)),
            ("Starmie", new Vector2(5.5f, 0f)),
        },
        // 4번방 — 흡인 합류. 강챙이가 가운데서 당기고 바깥에서 레이저가 조인다.
        ["F3Room4_Combat"] = new (string, Vector2)[]
        {
            ("Poliwrath", new Vector2(3f, 0f)),
            ("Kingler", new Vector2(1.8f, 2f)),
            ("Kingler", new Vector2(1.8f, -2f)),
            ("Dewgong", new Vector2(4.5f, 2.8f)),
            ("Dewgong", new Vector2(4.5f, -2.8f)),
            ("Starmie", new Vector2(5.6f, 1.5f)),
            ("Starmie", new Vector2(5.6f, -1.5f)),
        },
        // 5번방 — 엘리트 종합. 신뇽 해류 위에서 전 종류의 CC가 얽힌다.
        ["F3Room5_Combat"] = new (string, Vector2)[]
        {
            ("Dragonair", new Vector2(4.5f, 0f)),
            ("Kingler", new Vector2(2f, 0f)),
            ("Poliwrath", new Vector2(3f, 2.5f)),
            ("Dewgong", new Vector2(3f, -2.5f)),
            ("Starmie", new Vector2(5.5f, 2.8f)),
            ("Starmie", new Vector2(5.5f, -2.8f)),
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
                ("goldReward", spec.gold),
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
