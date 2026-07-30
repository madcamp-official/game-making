using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 파이리·꼬부기 계열의 <see cref="CharacterData"/>를 채우는 일회성 도구.
/// <see cref="PmdCharacterPipeline.ImportPlayerLines"/>로 컨트롤러·스프라이트를 구운 <b>뒤에</b>
/// 실행해야 한다 — 여기서 그 결과물을 이름으로 집어 연결하기 때문이다.
///
/// 다시 실행해도 안전하다: 기술 세트·단계를 통째로 다시 쓴다. 수치를 손보고 싶으면
/// Inspector에서 에셋을 직접 고치면 되고, 이 스크립트를 다시 돌리면 명세 기본값으로 돌아온다.
///
/// 마지막에 <b>fallbackCharacter를 비운다</b> — 이 연결이 남아 있는 동안 파이리·꼬부기는
/// 이상해씨로 시작했다. 자기 기술·단계가 채워졌으니 폴백은 더 이상 쓸 일이 없다.
/// </summary>
public static class CharacterLineSetup
{
    private const string CharacterDir = "Assets/Game/Data/Characters/";
    private const string ArtDir = "Assets/Game/Art/Characters/";

    [MenuItem("Game/파이리·꼬부기 계열 데이터 굽기")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine(BuildCharmander());
        log.AppendLine(BuildSquirtle());
        log.AppendLine(RetunePetalDance());
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ---------------------------------------------------------------- 파이리 (리자몽 계열)

    private static string BuildCharmander()
    {
        CharacterData data = Load("Charmander");
        if (data == null) return "Charmander.asset이 없다";

        data.displayName = "파이리";
        // 수치가 아니라 "어떻게 싸우는가"만 적는다 — CharacterData.playStyle의 규칙이다.
        data.playStyle = "체력은 낮지만 화력이 높다. 원거리에서 불을 퍼붓는다";

        data.moveSet = new PlayerMoveSet
        {
            startingMoveCount = 2,
            moves = new[]
            {
                Move(MoveType.FireSpit, "불꽃세례",
                    "겨눈 방향으로 불덩이를 쏜다. 적이나 벽에 닿으면 사라진다.",
                    AttackKind.Ranged, "",
                    MoveUpgradeId.FireSpitDamage, MoveUpgradeId.FireSpitSize, MoveUpgradeId.FireSpitCooldown),
                Move(MoveType.DragonDance, "용의춤",
                    "잠시 공격력과 이동 속도가 오른다.",
                    AttackKind.None, "버프",
                    MoveUpgradeId.DancePower, MoveUpgradeId.DanceSpeed, MoveUpgradeId.DanceDuration),
                Move(MoveType.DragonClaw, "드래곤클로",
                    "겨눈 방향을 발톱으로 크게 후려친다. 범위 안의 적이 한꺼번에 맞는다.",
                    AttackKind.Melee, "",
                    MoveUpgradeId.ClawDamage, MoveUpgradeId.ClawRadius, MoveUpgradeId.ClawKnockback),
                Move(MoveType.Flamethrower, "화염방사",
                    "잠시 동안 마우스를 따라 도는 화염 줄기를 뿜는다. 뿜는 동안 걸음이 느려진다.",
                    AttackKind.Ranged, "",
                    MoveUpgradeId.FlameDamage, MoveUpgradeId.FlameWidth, MoveUpgradeId.FlameCooldown),
            },
        };

        // 슬롯 순서(불꽃세례·용의춤·드래곤클로·화염방사)대로 적는 단계별 기준 위력.
        // 용의춤은 피해가 없어 0이고, 화염방사는 틱당 피해라 전 단계 14로 같다 (총 6틱 84).
        data.stages = new[]
        {
            Stage("파이리", "Charmander", 85, new[] { 12, 0, 26, 14 }),
            Stage("리자드", "Charmeleon", 105, new[] { 18, 0, 38, 14 }),
            Stage("리자몽", "Charizard", 130, new[] { 25, 0, 52, 14 }),
        };

        return Finish(data, "Charmander");
    }

    // ---------------------------------------------------------------- 꼬부기 (거북왕 계열)

    private static string BuildSquirtle()
    {
        CharacterData data = Load("Squirtle");
        if (data == null) return "Squirtle.asset이 없다";

        data.displayName = "꼬부기";
        data.playStyle = "돌진과 방어로 근거리·원거리를 오가며 싸운다";

        data.moveSet = new PlayerMoveSet
        {
            startingMoveCount = 2,
            moves = new[]
            {
                // 이름은 물대포지만 구현·판정은 몸통박치기와 같다 — 그래서 근접이다.
                Move(MoveType.WaterGun, "물대포",
                    "겨눈 방향에 물줄기를 터뜨리는 근접 공격.",
                    AttackKind.Melee, "",
                    MoveUpgradeId.WaterGunDamage, MoveUpgradeId.WaterGunRadius, MoveUpgradeId.WaterGunCooldown),
                Move(MoveType.Surf, "파도타기",
                    "겨눈 방향으로 파도를 타고 돌진하며 길에 닿은 적을 때린다. 벽에 닿으면 멈춘다.",
                    AttackKind.Melee, "",
                    MoveUpgradeId.SurfDamage, MoveUpgradeId.SurfDistance, MoveUpgradeId.SurfCooldown),
                Move(MoveType.RocketHeadbutt, "로켓박치기",
                    "잠깐 웅크렸다가 로켓처럼 돌진한다. 돌진하는 동안에는 무적이다.",
                    AttackKind.Melee, "",
                    MoveUpgradeId.RocketDamage, MoveUpgradeId.RocketKnockback, MoveUpgradeId.RocketCooldown),
                Move(MoveType.HydroPump, "하이드로펌프",
                    "자리에 버티고 서서 굵은 물줄기를 뿜는다. 뿜는 동안 받는 피해가 줄어든다.",
                    AttackKind.Ranged, "",
                    MoveUpgradeId.HydroDamage, MoveUpgradeId.HydroWidth, MoveUpgradeId.HydroGuard),
            },
        };

        // 하이드로펌프는 틱당 피해라 전 단계 8로 같다 (총 12틱 96).
        data.stages = new[]
        {
            Stage("꼬부기", "Squirtle", 95, new[] { 10, 12, 28, 8 }),
            Stage("어니부기", "Wartortle", 120, new[] { 15, 18, 40, 8 }),
            Stage("거북왕", "Blastoise", 150, new[] { 21, 25, 55, 8 }),
        };

        return Finish(data, "Squirtle");
    }

    // ---------------------------------------------------------------- 이상해씨 손질

    /// <summary>
    /// 꽃잎댄스를 원거리에서 <b>근접</b>으로 바꾼다. 리자몽 계열이 원거리 화력을 맡으면서
    /// 이상해꽃은 근접 전투와 회복으로 몫을 갈랐다 — 몸을 따라다니는 장판이니 판정도
    /// 그쪽이 자연스럽다. 구애머리띠·시라소몬 수련(근접 배율)이 이제 꽃잎댄스에 걸린다.
    /// </summary>
    private static string RetunePetalDance()
    {
        CharacterData data = Load("Bulbasaur");
        if (data == null) return "Bulbasaur.asset이 없다";

        PlayerMoveDefinition petal = data.moveSet?.Find(MoveType.PetalDance);
        if (petal == null) return "이상해씨 기술 세트에 꽃잎댄스가 없다";
        petal.attackKind = AttackKind.Melee;
        EditorUtility.SetDirty(data);
        return "이상해씨: 꽃잎댄스 속성을 근접으로";
    }

    // ---------------------------------------------------------------- 조립 부품

    private static PlayerMoveDefinition Move(MoveType type, string name, string summary,
                                             AttackKind kind, string tag, params MoveUpgradeId[] upgrades)
    {
        return new PlayerMoveDefinition
        {
            type = type,
            displayName = name,
            summary = summary,
            attackKind = kind,
            tagOverride = tag,
            upgrades = upgrades,
        };
    }

    /// <summary>
    /// 진화 단계 하나. 컨트롤러는 파이프라인이 구운 것을 쓰고, 초상은 이상해씨 계열과 같은
    /// 규칙 — 남쪽 대기 1프레임(Idle_0_0) — 을 따른다.
    /// </summary>
    private static PlayerEvolution.Stage Stage(string koreanName, string species,
                                              int maxHealth, int[] movePowers)
    {
        return new PlayerEvolution.Stage
        {
            stageName = koreanName,
            animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ArtDir + species + "/" + species + ".controller"),
            portrait = SouthIdleFrame(species),
            maxHealth = maxHealth,
            movePowers = movePowers,
        };
    }

    private static Sprite SouthIdleFrame(string species)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ArtDir + species + "/Sprites/Idle.png")
            .OfType<Sprite>().FirstOrDefault(s => s.name == "Idle_0_0");
    }

    /// <summary>선택 화면 연결을 채우고, 폴백을 끊고, 빠진 조각이 없는지 마지막으로 살핀다.</summary>
    private static string Finish(CharacterData data, string firstSpecies)
    {
        data.portrait = data.stages[0].portrait;
        data.previewController = data.stages[0].animatorController;
        data.previewHoverState = "Walk_0";
        data.previewIdleState = "Idle_0";
        data.fallbackCharacter = null;
        EditorUtility.SetDirty(data);

        var missing = new System.Text.StringBuilder();
        foreach (PlayerEvolution.Stage stage in data.stages)
        {
            if (stage.animatorController == null) missing.Append(" 컨트롤러:" + stage.stageName);
            if (stage.portrait == null) missing.Append(" 초상:" + stage.stageName);
        }
        return data.displayName + ": 기술 " + data.moveSet.Count + "개, 단계 " + data.stages.Length
             + "개, 폴백 해제" + (missing.Length > 0 ? " — 빠짐:" + missing : "");
    }

    private static CharacterData Load(string name) =>
        AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterDir + name + ".asset");
}
