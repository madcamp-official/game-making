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
    /// 때리는 넷. 사거리는 몸집과 무기 길이를 따른다 — 스라크의 낫이 가장 길고,
    /// 단데기는 몸을 부딪는 것이라 가장 짧다.
    /// </summary>
    private static readonly MeleeSpec[] Specs =
    {
        // 1층 — 첫 층이라 한 대가 아프면 안 된다. 짧게, 자주, 약하게.
        new MeleeSpec { species = "Caterpie", thirdParty = "0010_Caterpie", anim = "Attack",
                        hitDelay = 0.27f, reach = 1.1f, sweepAngle = 120f,
                        damage = 5, cooldown = 2.0f, initialDelay = 1.0f, recovery = 0.3f },
        new MeleeSpec { species = "Metapod", thirdParty = "0011_Metapod", anim = "Attack",
                        hitDelay = 0.13f, reach = 1.0f, sweepAngle = 140f,
                        damage = 6, cooldown = 2.4f, initialDelay = 1.4f, recovery = 0.35f },
        // 스라크는 1층의 정예다. 낫이 길고 아프며, 그만큼 예비 동작이 길어 읽을 수 있다.
        new MeleeSpec { species = "Scyther", thirdParty = "0123_Scyther", anim = "Slice",
                        hitDelay = 0.15f, reach = 1.9f, sweepAngle = 150f,
                        damage = 11, cooldown = 2.2f, initialDelay = 1.2f, recovery = 0.4f },
        // 3층 강챙이 — 흡인으로 끌어당긴 뒤 이걸로 때린다. 붙어 있는 시간이 짧아 쿨이 짧다.
        new MeleeSpec { species = "Poliwrath", thirdParty = "0062_Poliwrath", anim = "Attack",
                        hitDelay = 0.15f, reach = 1.4f, sweepAngle = 120f,
                        damage = 12, cooldown = 2.6f, initialDelay = 2.6f, recovery = 0.35f },
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
        // 발동 조건: 사거리보다 살짝 넓게 잡아, 다가오는 도중에 동작이 시작되게 한다.
        so.FindProperty("range").floatValue = spec.reach + 0.4f;
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
