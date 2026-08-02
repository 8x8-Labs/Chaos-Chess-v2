using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 세이브 파일을 "쓰다 만 상태"가 남지 않게 기록하고, 손상 시 이전 세대로 복구하는 유틸리티.
///
/// <para>
/// <b>왜 필요한가</b> — <c>File.WriteAllText</c>는 원자적이지 않다. 모바일에서 OS가
/// 앱을 강제 종료하면 잘린 JSON이 그대로 남고, 다음 실행에서 세이브 전체를 잃는다.
/// 홈 버튼 → 백그라운드 정리는 흔한 시나리오라 실제로 발생한다.
/// </para>
///
/// <para>
/// <b>쓰기 절차</b> (원본을 마지막 순간까지 건드리지 않는다)
/// <list type="number">
/// <item><description><c>path.tmp</c>에 전체를 기록</description></item>
/// <item><description>기존 <c>path</c>를 <c>path.bak</c>으로 이동 (한 세대 보존)</description></item>
/// <item><description><c>path.tmp</c>를 <c>path</c>로 이동</description></item>
/// </list>
/// 각 단계는 rename이라 사실상 원자적이다.
/// </para>
///
/// <para>
/// <b>중단 지점별 결과</b>
/// <list type="bullet">
/// <item><description>1단계 중 종료 → 원본 <c>path</c> 그대로 살아 있음</description></item>
/// <item><description>2~3단계 사이 종료 → <c>path</c>는 없지만 <c>path.tmp</c>(신규)와
/// <c>path.bak</c>(직전)이 둘 다 온전함</description></item>
/// </list>
/// 그래서 읽기는 <c>path</c> → <c>path.tmp</c> → <c>path.bak</c> 순으로 시도한다.
/// </para>
///
/// <para>
/// <b>한계</b> — 이건 손상 방지이지 보안이 아니다. 파일은 여전히 평문이며 사용자가 편집할 수 있다.
/// 변조 방지는 서버 권위 검증으로만 가능하다.
/// </para>
/// </summary>
public static class SafeFile
{
    private const string TempSuffix = ".tmp";
    private const string BackupSuffix = ".bak";

    public static string TempPathOf(string path) => path + TempSuffix;
    public static string BackupPathOf(string path) => path + BackupSuffix;

    /// <summary>
    /// 읽어낼 수 있는 세이브가 하나라도 있는지 확인한다.
    /// 본 파일이 없어도 복구 후보가 남아 있으면 true다.
    /// </summary>
    public static bool Exists(string path)
    {
        return File.Exists(path) || File.Exists(TempPathOf(path)) || File.Exists(BackupPathOf(path));
    }

    /// <summary>
    /// 임시 파일에 먼저 쓰고 rename으로 교체한다. 예외는 호출부로 전파하지 않고 false를 반환한다.
    /// </summary>
    public static bool WriteAtomic(string path, string contents)
    {
        string temp = TempPathOf(path);
        string backup = BackupPathOf(path);

        try
        {
            File.WriteAllText(temp, contents);

            if (File.Exists(path))
            {
                // File.Replace는 일부 플랫폼/런타임에서 지원이 불안정하다.
                // Delete + Move만 쓰면 어디서든 동일하게 동작한다.
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(path, backup);
            }

            File.Move(temp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"SafeFile.WriteAtomic: '{path}' 기록 실패 - {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 세이브를 읽는다. 본 파일이 없거나 <paramref name="isValid"/>를 통과하지 못하면
    /// 임시본 → 백업본 순으로 복구를 시도한다.
    /// </summary>
    /// <param name="isValid">
    /// 내용이 온전한지 판정하는 함수. null이면 "비어 있지 않으면 통과"로 간주한다.
    /// 파싱 예외는 내부에서 잡아 "실패"로 처리하므로 그냥 파싱해서 던져도 된다.
    /// </param>
    public static bool TryRead(string path, Func<string, bool> isValid, out string contents)
    {
        string[] candidates = { path, TempPathOf(path), BackupPathOf(path) };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!TryReadCandidate(candidates[i], isValid, out contents)) continue;

            if (i > 0)
                Debug.LogWarning($"SafeFile.TryRead: '{path}'가 손상되어 '{candidates[i]}'에서 복구했습니다.");

            return true;
        }

        contents = null;
        return false;
    }

    private static bool TryReadCandidate(string path, Func<string, bool> isValid, out string contents)
    {
        contents = null;

        try
        {
            if (!File.Exists(path)) return false;

            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 판정 함수가 던지는 예외(예: 잘린 JSON 파싱 실패)도 "손상"으로 취급한다.
            if (isValid != null && !isValid(text)) return false;

            contents = text;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SafeFile: '{path}' 읽기 실패 - {e.Message}");
            return false;
        }
    }

    /// <summary>본 파일과 복구 후보(임시본·백업본)를 모두 지운다.</summary>
    public static void Delete(string path)
    {
        foreach (string target in new[] { path, TempPathOf(path), BackupPathOf(path) })
        {
            try
            {
                if (File.Exists(target)) File.Delete(target);
            }
            catch (Exception e)
            {
                Debug.LogError($"SafeFile.Delete: '{target}' 삭제 실패 - {e.Message}");
            }
        }
    }

    /// <summary>
    /// 읽기 후보 중 가장 최근 수정 시각(UTC)을 반환한다. 하나도 없으면 <see cref="DateTime.MinValue"/>.
    /// </summary>
    public static DateTime GetLastWriteUtc(string path)
    {
        DateTime latest = DateTime.MinValue;

        foreach (string target in new[] { path, TempPathOf(path), BackupPathOf(path) })
        {
            try
            {
                if (!File.Exists(target)) continue;

                DateTime t = File.GetLastWriteTimeUtc(target);
                if (t > latest) latest = t;
            }
            catch (Exception e)
            {
                Debug.LogError($"SafeFile.GetLastWriteUtc: '{target}' 조회 실패 - {e.Message}");
            }
        }

        return latest;
    }
}
