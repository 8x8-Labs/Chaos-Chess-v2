using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 빌드 직전 Git의 최신 태그를 읽어 PlayerSettings.bundleVersion에 각인한다.
// "태그 = 단일 진실 출처" → release-drafter가 찍은 v0.3.1과 빌드 버전이 항상 일치.
// 런타임에서는 Application.version 으로 읽으면 된다.
//
// 빌드가 끝나면 bundleVersion을 원래 값으로 되돌린다. 각인 값은 빌드 산출물에만
// 들어가면 충분한데, 디스크의 ProjectSettings.asset에 남겨두면 매 빌드마다 파일이
// 수정되어 작업 트리가 dirty가 되고, 그 dirty 때문에 다음 빌드의 git describe가
// "-dirty"를 붙이는 악순환이 생긴다.
public class VersionStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private string previousVersion;
    private bool stamped;

    public void OnPreprocessBuild(BuildReport report)
    {
        string version = GitVersion.Resolve();
        if (string.IsNullOrEmpty(version))
        {
            UnityEngine.Debug.LogWarning(
                "[VersionStamp] Git 태그를 읽지 못해 bundleVersion을 그대로 둡니다.");
            return;
        }

        previousVersion = PlayerSettings.bundleVersion;
        PlayerSettings.bundleVersion = version;
        stamped = true;
        UnityEngine.Debug.Log($"[VersionStamp] bundleVersion = {version}");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!stamped)
            return;

        // 빌드 산출물에는 이미 각인된 값이 들어갔으므로, 프로젝트 파일은 원복해
        // ProjectSettings.asset이 dirty로 남지 않게 한다.
        PlayerSettings.bundleVersion = previousVersion;
        AssetDatabase.SaveAssets();
        stamped = false;
        UnityEngine.Debug.Log($"[VersionStamp] bundleVersion 원복 = {previousVersion}");
    }
}
