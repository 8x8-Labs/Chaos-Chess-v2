using System.Diagnostics;
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
        string version = ResolveVersionFromGit();
        if (string.IsNullOrEmpty(version))
        {
            UnityEngine.Debug.LogWarning(
                "[VersionStamp] Git 태그를 읽지 못해 bundleVersion을 그대로 둡니다.");
            return;
        }

        PlayerSettings.bundleVersion = version;
        UnityEngine.Debug.Log($"[VersionStamp] bundleVersion = {version}");
    }

    // 예: 태그 v0.3.1 위 → "0.3.1", 태그 이후 2커밋 → "0.3.1+2.g1a2b3c"
    private static string ResolveVersionFromGit()
    {
        string raw = RunGit("describe --tags --always --dirty");
        if (string.IsNullOrEmpty(raw))
            return null;

        raw = raw.Trim().TrimStart('v', 'V');

        // git describe 형식 "0.3.1-2-g1a2b3c" → SemVer 빌드 메타데이터 형식으로 정규화
        int firstDash = raw.IndexOf('-');
        if (firstDash < 0)
            return raw;

        string baseVersion = raw.Substring(0, firstDash);
        string meta = raw.Substring(firstDash + 1).Replace('-', '.');
        return $"{baseVersion}+{meta}";
    }

    private static string RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Application.dataPath, // .../Assets — git이 상위로 거슬러 찾음
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            // git 미설치 등 — 빌드를 막지 않는다.
            return null;
        }
    }
}
