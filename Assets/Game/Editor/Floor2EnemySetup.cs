using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2층 적 다섯 종의 프리팹을 만들고 전투방에 배치하는 일회성 설정 스크립트.
/// 수치의 원본이 여기다 — 프리팹을 다시 만들 일이 있으면 값을 여기서 고치고 재실행한다.
///
/// 프리팹은 스라크(Enemy_Scyther)를 본으로 뜬다. 콜라이더·머티리얼·체력바 등
/// 공통 구성을 그대로 물려받고, 스프라이트·컨트롤러·수치·능력만 갈아 끼운다.
/// </summary>
public static class Floor2EnemySetup
{
    private class EnemySpec
    {
        public string name;             // Enemy_{name}.prefab, Characters/{name}/
        public int health;
        public float scale;
        public float moveSpeed;
        public int contactDamage;
        public int gold;
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
        // 고지 — 방어형 전위. 붙으면 정면을 할퀴고 물러나 웅크린다.
        new EnemySpec
        {
            name = "Sandslash", health = 140, scale = 1.25f, moveSpeed = 3.6f,
            contactDamage = 12, gold = 12, knockbackMultiplier = 0.6f,
            ability = typeof(EnemyGuardAbility),
            abilityValues = new (string, object)[]
            {
                ("range", 2.3f), ("cooldown", 4f), ("initialDelay", 1f),
            },
        },
        // 텅구리 — 중거리 견제. 거리를 지키며 왕복하는 뼈를 던진다.
        new EnemySpec
        {
            name = "Marowak", health = 100, scale = 1.2f, moveSpeed = 3f,
            contactDamage = 10, gold = 12, keepDistance = 3.2f,
            ability = typeof(EnemyBoomerangAbility),
            abilityValues = new (string, object)[]
            {
                ("range", 6.5f), ("minRange", 1.2f), ("cooldown", 3.5f), ("initialDelay", 1f),
            },
        },
        // 닥트리오 — 지중 기습. 걸어다니지 않고(기본 AI 꺼짐) 제자리에 있다가 파고든다.
        new EnemySpec
        {
            name = "Dugtrio", health = 95, scale = 1.2f, moveSpeed = 2.5f,
            contactDamage = 10, gold = 12, knockbackMultiplier = 0.5f, basicAI = false,
            boxSize = new Vector2(0.9f, 0.55f),
            ability = typeof(EnemyBurrowAbility),
            abilityValues = new (string, object)[]
            {
                // 사거리 = 방 전체. 잠수가 유일한 이동 수단이라, 사거리 밖이면 조각상이 되어
                // 어그로가 풀린 것처럼 보인다.
                ("range", 20f), ("cooldown", 4.5f), ("initialDelay", 1.4f),
            },
        },
        // 나인테일 — 공간 통제. 멀찍이 물러서서 긴 화염 줄기로 길을 막는다.
        new EnemySpec
        {
            name = "Ninetales", health = 90, scale = 1.25f, moveSpeed = 3.4f,
            contactDamage = 10, gold = 14, keepDistance = 4.2f,
            ability = typeof(EnemyFlameLineAbility),
            abilityValues = new (string, object)[]
            {
                ("range", 7.5f), ("minRange", 1f), ("cooldown", 4.5f), ("initialDelay", 1.2f),
                ("flameDuration", 5f),
            },
        },
        // 데구리 — 엘리트. 코뿌리의 이판사판을 닮은, 웅크려 구르는 긴 돌진.
        new EnemySpec
        {
            name = "Graveler", health = 180, scale = 1.3f, moveSpeed = 3.2f,
            contactDamage = 14, gold = 20, knockbackMultiplier = 0.3f,
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
    private static readonly Dictionary<string, (string enemy, Vector2 at)[]> Rooms =
        new Dictionary<string, (string, Vector2)[]>
    {
        // 1번방 — 방어 타이밍과 왕복 투사체 소개. 전위 둘 뒤에 투척수 둘.
        ["F2Room1_Combat"] = new (string, Vector2)[]
        {
            ("Sandslash", new Vector2(2f, 1.5f)),
            ("Sandslash", new Vector2(2f, -1.5f)),
            ("Marowak", new Vector2(4.8f, 2.5f)),
            ("Marowak", new Vector2(4.8f, -2.5f)),
        },
        // 2번방 — 현재 위치(전위·투척)와 이동 경로(지중)를 동시에 봐야 한다.
        ["F2Room2_Combat"] = new (string, Vector2)[]
        {
            ("Sandslash", new Vector2(1.5f, 2f)),
            ("Sandslash", new Vector2(1.5f, -2f)),
            ("Dugtrio", new Vector2(3.5f, 3.2f)),
            ("Dugtrio", new Vector2(3.5f, -3.2f)),
            ("Marowak", new Vector2(5.2f, 1.5f)),
            ("Marowak", new Vector2(5.2f, -1.5f)),
        },
        // 4번방 — 방어형 전위 뒤에서 공간 통제.
        ["F2Room4_Combat"] = new (string, Vector2)[]
        {
            ("Sandslash", new Vector2(2f, 2f)),
            ("Sandslash", new Vector2(2f, -2f)),
            ("Dugtrio", new Vector2(4f, 3.2f)),
            ("Dugtrio", new Vector2(4f, -3.2f)),
            ("Ninetales", new Vector2(5.5f, 0f)),
        },
        // 5번방 — 엘리트 혼합 전투.
        ["F2Room5_Combat"] = new (string, Vector2)[]
        {
            ("Graveler", new Vector2(2.5f, 2.2f)),
            ("Graveler", new Vector2(2.5f, -2.2f)),
            ("Ninetales", new Vector2(5.3f, 3f)),
            ("Ninetales", new Vector2(5.3f, -3f)),
            ("Marowak", new Vector2(5.8f, 0f)),
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
                ("attackDamage", spec.contactDamage),
                ("goldReward", spec.gold),
                ("keepDistance", spec.keepDistance),
                ("knockbackMultiplier", spec.knockbackMultiplier),
                ("basicAIEnabled", spec.basicAI));

            // 본에 붙어 있던 스라크의 돌진을 떼고 전용 능력을 단다.
            // 데구리도 같은 돌진을 쓰지만 수치가 전부 달라 새로 붙이는 쪽이 깨끗하다.
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
                // 임시 배치였던 사막 잡몹을 전부 걷어낸다.
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
