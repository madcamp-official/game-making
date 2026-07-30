using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 흐름 화면이 쓸 에셋을 갖추고 씬에 <see cref="GameFlow"/>를 세운다.
///
/// 1단계: 대화창 스프라이트를 9슬라이스로 들인다.
/// 2단계: 지금 씬의 플레이어에게 굳어 있던 진화 단계를 <see cref="CharacterData"/>로 떠내고,
///        씬에 GameFlow를 붙여 캐릭터 목록을 물린다.
/// 단계마다 다른 명령으로, 에디트 모드에서 실행할 것.
/// </summary>
public static class GameFlowSetup
{
    private const string CharacterDir = "Assets/Game/Data/Characters";

    // ---------------------------------------------------------------- 1단계

    /// <summary>
    /// UI 스프라이트를 들인다. 대화창 하나만 쓰던 시절에는 여기서 직접 임포트했는데,
    /// 버튼·기술칸·체력바까지 늘어나면서 테두리 두께 표를 <see cref="UiSpriteSetup"/>
    /// 한 군데로 모았다. 두께가 두 곳에 적혀 있으면 한쪽만 고쳐 놓고 창이 늘어지는 것을
    /// 한참 못 찾는다.
    /// </summary>
    public static string ImportPanelSprite() => UiSpriteSetup.ImportAll();

    // ---------------------------------------------------------------- 2단계

    /// <summary>
    /// 지금 씬 플레이어의 진화 단계를 이상해씨 <see cref="CharacterData"/>로 떠내고,
    /// GameFlow를 세워 캐릭터 목록을 물린다.
    /// </summary>
    public static string SetupScene()
    {
        if (!AssetDatabase.IsValidFolder(CharacterDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Data"))
                AssetDatabase.CreateFolder("Assets/Game", "Data");
            AssetDatabase.CreateFolder("Assets/Game/Data", "Characters");
        }

        var evolution = Object.FindAnyObjectByType<PlayerEvolution>();
        if (evolution == null) return "씬에서 PlayerEvolution을 찾지 못했다";

        // 이상해씨 — 이미 씬에 굳어 있는 단계를 그대로 떠낸다.
        var so = new SerializedObject(evolution);
        SerializedProperty stagesProp = so.FindProperty("stages");
        CharacterData bulbasaur = LoadOrCreate("Bulbasaur");
        bulbasaur.displayName = "이상해씨";
        bulbasaur.playStyle = "안정적인 근거리·범위 공격";
        CopyStages(stagesProp, bulbasaur);
        bulbasaur.portrait = FirstPortrait(bulbasaur);
        bulbasaur.previewController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Game/Art/Characters/Bulbasaur/Bulbasaur.controller");
        EditorUtility.SetDirty(bulbasaur);

        // 파이리·꼬부기 — 자리만 만든다. 스프라이트와 기술은 다음 단계에서 채운다.
        CharacterData charmander = LoadOrCreate("Charmander");
        charmander.displayName = "파이리";
        charmander.playStyle = "높은 공격력·화상으로 지속 피해";
        EditorUtility.SetDirty(charmander);

        CharacterData squirtle = LoadOrCreate("Squirtle");
        squirtle.displayName = "꼬부기";
        squirtle.playStyle = "높은 생존력·밀치기와 방어";
        EditorUtility.SetDirty(squirtle);

        // GameFlow — 씬에 하나. RoomFlowController와 같은 오브젝트에 두면 찾기 쉽다.
        var flowController = Object.FindAnyObjectByType<RoomFlowController>();
        if (flowController == null) return "씬에서 RoomFlowController를 찾지 못했다";

        GameFlow flow = flowController.GetComponent<GameFlow>();
        if (flow == null) flow = flowController.gameObject.AddComponent<GameFlow>();

        var flowSo = new SerializedObject(flow);
        SerializedProperty list = flowSo.FindProperty("characters");
        var all = new List<CharacterData> { bulbasaur, charmander, squirtle };
        list.arraySize = all.Count;
        for (int i = 0; i < all.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        flowSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(flowController.gameObject.scene);
        EditorSceneManager.SaveScene(flowController.gameObject.scene);
        return "CharacterData 3개 준비, GameFlow를 " + flowController.name + "에 붙임 (씬 저장)";
    }

    private static CharacterData LoadOrCreate(string name)
    {
        string path = CharacterDir + "/" + name + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        if (data != null) return data;
        data = ScriptableObject.CreateInstance<CharacterData>();
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    /// <summary>
    /// 씬에 굳어 있는 진화 단계를 에셋으로 베낀다. SerializedProperty로 옮기는 이유:
    /// Stage는 클래스라 그냥 대입하면 씬과 에셋이 같은 것을 가리켜, 한쪽을 고치면 둘 다 바뀐다.
    /// </summary>
    private static void CopyStages(SerializedProperty source, CharacterData target)
    {
        int count = source.arraySize;
        var stages = new PlayerEvolution.Stage[count];
        for (int i = 0; i < count; i++)
        {
            SerializedProperty item = source.GetArrayElementAtIndex(i);
            stages[i] = new PlayerEvolution.Stage
            {
                stageName = item.FindPropertyRelative("stageName").stringValue,
                animatorController = (RuntimeAnimatorController)
                    item.FindPropertyRelative("animatorController").objectReferenceValue,
                portrait = (Sprite)item.FindPropertyRelative("portrait").objectReferenceValue,
                maxHealth = item.FindPropertyRelative("maxHealth").intValue,
                attackDamage = item.FindPropertyRelative("attackDamage").intValue,
                vineDamage = item.FindPropertyRelative("vineDamage").intValue,
            };
        }
        target.stages = stages;
    }

    private static Sprite FirstPortrait(CharacterData data) =>
        data.stages != null && data.stages.Length > 0 ? data.stages[0].portrait : null;
}
