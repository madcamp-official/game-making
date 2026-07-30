using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <c>docs/FINALCREDITS.md</c>를 게임이 읽을 수 있는 자리로 옮긴다.
///
/// 크레딧 문서가 둘인 것은 보는 사람이 다르기 때문이다. <c>CREDITS.md</c>는 <b>만드는 쪽</b>의
/// 장부라 파일 경로와 굽는 방법, 아직 출처를 못 찾은 것까지 적혀 있고, <c>FINALCREDITS.md</c>는
/// <b>플레이어가 보는</b> 두루마리다. 화면에 흘리는 것은 뒤쪽이다.
///
/// <c>docs/</c>는 빌드에 실리지 않아서 그대로 두면 배포본에서 크레딧이 통째로 사라진다 —
/// 저작자 표시는 PMDCollab(CC BY-NC)의 <b>라이선스 조건</b>이라 빠뜨릴 수 있는 것이 아니다.
/// 그래서 <c>Resources</c> 아래로 복사해 두고 <see cref="CreditsScreen"/>이 그것을 읽는다.
/// 문서를 고쳤으면 이 메뉴를 한 번 눌러 옮긴다. 복사본을 직접 고치지 않는다 — 다음 갱신에
/// 덮어써진다.
/// </summary>
public static class CreditsTextSetup
{
    private const string SourcePath = "docs/FINALCREDITS.md";
    private const string TargetPath = "Assets/Game/Resources/Text/Credits.txt";

    [MenuItem("Tools/크레딧 텍스트 갱신 (docs → Resources)")]
    public static void Sync()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, SourcePath);

        if (!File.Exists(source))
        {
            Debug.LogError("크레딧 원본을 찾지 못했다: " + source);
            return;
        }

        string body = File.ReadAllText(source);
        string target = Path.Combine(projectRoot, TargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target));

        // 내용이 같으면 건드리지 않는다. 괜히 다시 쓰면 임포트가 돌고 깃 기록도 지저분해진다.
        if (File.Exists(target) && File.ReadAllText(target) == body)
        {
            Debug.Log("크레딧 텍스트는 이미 최신이다: " + TargetPath);
            return;
        }

        File.WriteAllText(target, body);
        AssetDatabase.ImportAsset(TargetPath);
        Debug.Log("크레딧 텍스트를 갱신했다: " + TargetPath + " (" + body.Length + "자)");
    }
}
