using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 빌드 직전 Git의 최신 태그를 읽어 PlayerSettings.bundleVersion에 각인한다.
// "태그 = 단일 진실 출처" → release-drafter가 찍은 v0.3.1과 빌드 버전이 항상 일치.
// 런타임에서는 Application.version 으로 읽으면 된다.
public class VersionStamp : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string version = GitVersion.Resolve();
        if (string.IsNullOrEmpty(version))
        {
            UnityEngine.Debug.LogWarning(
                "[VersionStamp] Git 태그를 읽지 못해 bundleVersion을 그대로 둡니다.");
            return;
        }

        PlayerSettings.bundleVersion = version;
        UnityEngine.Debug.Log($"[VersionStamp] bundleVersion = {version}");
    }
}
