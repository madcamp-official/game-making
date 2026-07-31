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
        public float range;         // 중심 거리 기준 예비 검사 (reach와 재는 자가 다르다)
        public float sweepAngle;
        public int damage;
        public float cooldown;
        public float initialDelay;
        public float recovery;
        public bool onlyWhenLastEnemy; // 방에 혼자 남았을 때만 휘두른다
    }

    /// <summary>
    /// 때리는 넷.
    ///
    /// <c>reach</c>는 <b>몸 표면에서 더 뻗는 거리</b>다(중심 거리가 아니다). 맞는 거리이자
    /// <b>휘두르기 시작하는 거리</b>이기도 하다 — <see cref="EnemyMeleeAbility.ReadyToCast"/>가
    /// 같은 값으로 판단해서, 닿지 않는 자리에서는 아예 팔을 뻗지 않는다.
    ///
    /// 상한은 플레이어 몸통박치기가 닿는 거리다. 중심에서 0.9 앞에 반지름 0.85 원이라
    /// 중심 기준 1.75, 플레이어 콜라이더 반너비 0.3을 빼면 <b>표면 사이 1.45</b>다.
    /// 잡몹 근접기가 이보다 길면 근접전에서 플레이어가 먼저 손을 댈 방법이 없어진다.
    ///
    /// 각도도 넓다. 잡몹은 플레이어(5칸/초)보다 훨씬 느려서 — 캐터피 1.5, 강챙이 2.9,
    /// 스라크 3.8 — 좁은 부채꼴이면 옆으로 한 걸음만 돌아도 공짜로 빠져나간다. 대신
    /// 조준은 여전히 동작 시작에 고정하므로, <b>뒤로 빠지는</b> 회피는 그대로 통한다.
    ///
    /// <c>range</c>는 <b>중심 거리</b>로 재는 굵은 예비 검사라 <c>reach</c>에서 유도하지
    /// 않는다. 넉넉히 두어 적이 걸어오는 동안 미리 통과시켜 두고, 실제로 팔을 뻗을지는
    /// <c>reach</c>가 정한다. 좁히면 두 자로 두 번 거르는 셈이라 발동이 늦어진다.
    ///
    /// 다음 공격까지의 실제 간격은 recovery + cooldown이다 (쿨은 동작이 끝난 뒤부터 잰다).
    /// </summary>
    private static readonly MeleeSpec[] Specs =
    {
        // 1층 — 첫 층이라 한 대가 아프면 안 된다. 짧게, 자주, 약하게.
        new MeleeSpec { species = "Caterpie", thirdParty = "0010_Caterpie", anim = "Attack",
                        hitDelay = 0.27f, reach = 0.9f, range = 2.6f, sweepAngle = 210f,
                        damage = 5, cooldown = 1.0f, initialDelay = 1.0f, recovery = 0.25f },
        new MeleeSpec { species = "Metapod", thirdParty = "0011_Metapod", anim = "Attack",
                        hitDelay = 0.13f, reach = 0.8f, range = 2.1f, sweepAngle = 220f,
                        damage = 6, cooldown = 1.2f, initialDelay = 1.4f, recovery = 0.25f },
        // 스라크는 1층의 정예다. 낫이 길고 아프며, 발도 빨라 붙으면 쉽게 못 뗀다.
        new MeleeSpec { species = "Scyther", thirdParty = "0123_Scyther", anim = "Slice",
                        hitDelay = 0.15f, reach = 1.1f, range = 2.7f, sweepAngle = 200f,
                        damage = 11, cooldown = 1.1f, initialDelay = 1.2f, recovery = 0.3f },
        // 3층 강챙이 — 흡인으로 끌어당긴 뒤 이걸로 때린다.
        new MeleeSpec { species = "Poliwrath", thirdParty = "0062_Poliwrath", anim = "Attack",
                        hitDelay = 0.15f, reach = 1.0f, range = 2.4f, sweepAngle = 200f,
                        damage = 12, cooldown = 1.3f, initialDelay = 2.6f, recovery = 0.3f },
        // 3층 쥬레곤 — 원거리(냉기 숨결)가 주무기라, 붙었을 때의 근접기는 일부러 약하고,
        // 방에 혼자 남았을 때만 꺼내는 마지막 발악이다 (onlyWhenLastEnemy).
        // hitDelay는 AnimData Attack의 HitTime: HitFrame 3이 끝나는 (2+4+1+1)/60 = 0.13초.
        new MeleeSpec { species = "Dewgong", thirdParty = "0087_Dewgong", anim = "Attack",
                        hitDelay = 0.13f, reach = 1.0f, range = 2.5f, sweepAngle = 200f,
                        damage = 9, cooldown = 1.3f, initialDelay = 1.4f, recovery = 0.3f,
                        onlyWhenLastEnemy = true },
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
        so.FindProperty("range").floatValue = spec.range;
        so.FindProperty("cooldown").floatValue = spec.cooldown;
        so.FindProperty("initialDelay").floatValue = spec.initialDelay;
        so.FindProperty("actionState").stringValue = spec.anim;
        so.FindProperty("reach").floatValue = spec.reach;
        so.FindProperty("sweepAngle").floatValue = spec.sweepAngle;
        so.FindProperty("hitDelay").floatValue = spec.hitDelay;
        so.FindProperty("recovery").floatValue = spec.recovery;
        so.FindProperty("damage").intValue = spec.damage;
        so.FindProperty("onlyWhenLastEnemy").boolValue = spec.onlyWhenLastEnemy;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
