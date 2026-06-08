#if UNITY_EDITOR
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;

// Git 태그에서 SemVer 버전 문자열을 만든다.
// 빌드 훅(VersionStamp)과 에디터 표시(VersionLabel)가 동일한 규칙을 쓰도록
// 해석 로직을 한곳에 모은다. 에디터 전용 — 빌드된 런타임에는 포함되지 않는다.
public static class GitVersion
{
    // 해석 결과를 도메인 리로드 단위로 캐싱한다.
    // VersionLabel은 [ExecuteAlways]라 OnValidate/OnEnable 등으로 Refresh가 잦은데,
    // 그때마다 git 프로세스를 띄우면 에디터가 버벅인다(미설치 환경에선 매번 예외).
    // static은 스크립트 리컴파일(도메인 리로드) 시 자동 초기화되므로,
    // 버전이 바뀔 만한 시점(리컴파일)마다 자연히 다시 해석된다.
    private static bool _resolved;
    private static string _cached;

    // 예: 태그 v0.3.1 위 → "0.3.1", 태그 이후 2커밋 → "0.3.1+2.g1a2b3c"
    public static string Resolve()
    {
        if (_resolved)
            return _cached;

        _cached = ResolveFromGit();
        _resolved = true;
        return _cached;
    }

    private static string ResolveFromGit()
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
#endif
