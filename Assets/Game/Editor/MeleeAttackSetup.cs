using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1층·3층 잡몹의 접촉 피해를 없애고, 때리는 적에게만 눈에 보이는 근접기를 달아 준다.
///
/// 몸이 닿기만 하면 자동으로 깎이던 피해는 피할 방법도 피했다는 감각도 없었다. 그래서
/// 두 층 잡몹의 <c>attackDamage</c>를 전부 0으로 내리고(보스는 그대로 둔다 — 돌진과
/// 몸통 박치기가 패턴의 일부다), 네 종에게만 <see cref="EnemyMeleeAbility"/>를 단다.
///
/// 동작은 SpriteCollab의 실제 공격 시트를 쓴다. 타격 시점은 그 시트 AnimData의 HitFrame
/// 시간에 맞춰 두었다 — 휘두르는 그림과 판정이 어긋나면 안 된다.
///
/// 1단계와 2단계는 서로 다른 명령으로, 에디트 모드에서 실행할 것.
/// </summary>
public static class MeleeAttackSetup
{
    private class MeleeSpec
    {
        public string species;      // 게임 쪽 이름 = 프리팹 Enemy_{species}
        public string thirdParty;   // PMDCollab 폴더
        public string anim;         // 쓸 시트 이름 (게임 쪽 = 원본과 같다)
        public float hitDelay;      // AnimData의 HitFrame 시간
        public float reach;
        public float sweepAngle;
        public int damage;
        public float cooldown;
        public float initialDelay;
        public float recovery;
    }

    /// <summary>
    /// 때리는 넷.
    ///
    /// <c>reach</c>는 <b>몸 표면에서 더 뻗는 거리</b>다(중심 거리가 아니다). 기준은
    /// "휘두르는 동안 플레이어가 걸어 나가는 거리" — 플레이어 속도가 5칸/초이므로
    /// 5 x hitDelay다. 그만큼은 줘야 움직이는 상대에게 닿는다. 캐터피는 0.27초를
    /// 휘두르니 1.35칸, 나머지는 0.65~0.75칸이 최소선이고 거기에 여유를 조금 얹었다.
    ///
    /// 각도도 넓다. 잡몹은 플레이어(5칸/초)보다 훨씬 느려서 — 캐터피 1.5, 강챙이 2.9,
    /// 스라크 3.8 — 좁은 부채꼴이면 옆으로 한 걸음만 돌아도 공짜로 빠져나간다. 대신
    /// 조준은 여전히 동작 시작에 고정하므로, <b>뒤로 빠지는</b> 회피는 그대로 통한다.
    ///
    /// 다음 공격까지의 실제 간격은 recovery + cooldown이다 (쿨은 동작이 끝난 뒤부터 잰다).
    /// </summary>
    private static readonly MeleeSpec[] Specs =
    {
        // 1층 — 첫 층이라 한 대가 아프면 안 된다. 짧게, 자주, 약하게.
        new MeleeSpec { species = "Caterpie", thirdParty = "0010_Caterpie", anim = "Attack",
                        hitDelay = 0.27f, reach = 1.4f, sweepAngle = 210f,
                        damage = 5, cooldown = 1.0f, initialDelay = 1.0f, recovery = 0.25f },
        new MeleeSpec { species = "Metapod", thirdParty = "0011_Metapod", anim = "Attack",
                        hitDelay = 0.13f, reach = 0.9f, sweepAngle = 220f,
                        damage = 6, cooldown = 1.2f, initialDelay = 1.4f, recovery = 0.25f },
        // 스라크는 1층의 정예다. 낫이 길고 아프며, 발도 빨라 붙으면 쉽게 못 뗀다.
        new MeleeSpec { species = "Scyther", thirdParty = "0123_Scyther", anim = "Slice",
                        hitDelay = 0.15f, reach = 1.5f, sweepAngle = 200f,
                        damage = 11, cooldown = 1.1f, initialDelay = 1.2f, recovery = 0.3f },
        // 3층 강챙이 — 흡인으로 끌어당긴 뒤 이걸로 때린다.
        new MeleeSpec { species = "Poliwrath", thirdParty = "0062_Poliwrath", anim = "Attack",
                        hitDelay = 0.15f, reach = 1.2f, sweepAngle = 200f,
                        damage = 12, cooldown = 1.3f, initialDelay = 2.6f, recovery = 0.3f },
    };

    /// <summary>접촉 피해를 걷어낼 잡몹. 보스는 목록에 없다.</summary>
    private static readonly string[] ContactDamageOff =
    {
        // 1층
        "Caterpie", "Metapod", "Bellsprout", "Venonat", "Scyther",
        // 3층
        "Kingler", "Poliwrath", "Dewgong", "Starmie", "Dragonair",
    };

    // ---------------------------------------------------------------- 1단계 · 동작 들이기

    public static string ImportAnims()
    {
        var log = new StringBuilder();
        foreach (MeleeSpec spec in Specs)
            log.AppendLine(PmdCharacterPipeline.AddAnim(
                spec.species, spec.thirdParty,
                new PmdCharacterPipeline.AnimSpec(spec.anim, spec.anim, false)));
        return log.ToString();
    }

    // ---------------------------------------------------------------- 2단계 · 프리팹 손보기

    public static string ApplyToPrefabs()
    {
        var log = new StringBuilder();

        foreach (string species in ContactDamageOff)
        {
            string path = "Assets/Game/Prefabs/Enemies/Enemy_" + species + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller = root.GetComponent<EnemyController>();
                if (controller == null) { log.AppendLine(species + ": EnemyController 없음"); continue; }

                var so = new SerializedObject(controller);
                int before = so.FindProperty("attackDamage").intValue;
                so.FindProperty("attackDamage").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();

                MeleeSpec melee = Array.Find(Specs, s => s.species == species);
                if (melee != null) Attach(root, melee);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine(species + ": 접촉 피해 " + before + " → 0" +
                               (melee != null ? ", 근접기 " + melee.anim + " " + melee.damage : ""));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private static void Attach(GameObject root, MeleeSpec spec)
    {
        EnemyMeleeAbility melee = root.GetComponent<EnemyMeleeAbility>();
        if (melee == null) melee = root.AddComponent<EnemyMeleeAbility>();

        var so = new SerializedObject(melee);
        // 발동 조건만은 중심 거리로 잰다(EnemyAbility가 그렇게 판정한다). reach는 표면
        // 기준이므로 두 몸의 반지름만큼(대략 1.2칸) 더해 줘야 몸이 닿은 순간에 발동한다.
        so.FindProperty("range").floatValue = spec.reach + 1.2f;
        so.FindProperty("cooldown").floatValue = spec.cooldown;
        so.FindProperty("initialDelay").floatValue = spec.initialDelay;
        so.FindProperty("actionState").stringValue = spec.anim;
        so.FindProperty("reach").floatValue = spec.reach;
        so.FindProperty("sweepAngle").floatValue = spec.sweepAngle;
        so.FindProperty("hitDelay").floatValue = spec.hitDelay;
        so.FindProperty("recovery").floatValue = spec.recovery;
        so.FindProperty("damage").intValue = spec.damage;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
