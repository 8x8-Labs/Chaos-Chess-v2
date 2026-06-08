using System.Diagnostics;
using System.Text.RegularExpressions;
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

        // -dirty 접미사를 먼저 떼어낸다 (작업트리에 커밋 안 된 변경이 있을 때 git이 붙임).
        bool isDirty = raw.EndsWith("-dirty");
        if (isDirty)
            raw = raw.Substring(0, raw.Length - "-dirty".Length);

        // git describe 형식 "<tag>-<count>-g<hash>"에서 커밋수/해시 접미사만 분리한다.
        // 프리릴리스 태그(예: 0.3.0-rc.1)의 "-rc.1"은 태그 일부이므로 건드리지 않는다.
        // → rc 버전이 SemVer 우선순위에서 정식판과 동일 취급되는 버그를 막는다.
        var match = Regex.Match(raw, @"^(.*)-(\d+)-g([0-9a-fA-F]+)$");
        string version = match.Success
            ? $"{match.Groups[1].Value}+{match.Groups[2].Value}.g{match.Groups[3].Value}"
            : raw;

        if (isDirty)
            version = version.Contains("+") ? $"{version}.dirty" : $"{version}+dirty";

        return version;
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

            // 타임아웃을 먼저 건다. 멈춘 프로세스를 죽이지 않고 ExitCode에 접근하면
            // InvalidOperationException이 나므로, 타임아웃 시 강제 종료 후 빠진다.
            if (!process.WaitForExit(3000))
            {
                process.Kill();
                return null;
            }

            // git describe 출력은 짧아 WaitForExit 이후 읽어도 버퍼 데드락 위험이 없다.
            string output = process.StandardOutput.ReadToEnd();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            // git 미설치 등 — 빌드를 막지 않는다.
            return null;
        }
    }
}
