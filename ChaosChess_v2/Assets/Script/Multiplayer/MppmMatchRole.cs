using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Multiplayer.Playmode;
#endif

/// <summary>
/// Multiplayer Play Mode(MPPM)로 띄운 두 에디터 인스턴스에 역할과 진영을 자동으로 나눠 주는
/// **에디터 전용 테스트 보조 코드**입니다.
///
/// MPPM 가상 플레이어는 같은 씬 에셋을 공유하므로 인스펙터에 박아 둔 값으로는
/// 두 인스턴스를 구분할 수 없습니다. 그래서 런타임에 메인 에디터인지 클론인지로 갈라냅니다.
///
///   메인 에디터 → Host  + 백
///   클론        → Guest + 흑
///
/// join code도 UI가 없어 전달할 방법이 없으므로, 같은 머신이라는 점을 이용해
/// OS 임시 폴더의 파일로 주고받습니다.
///
/// 5단계 연결 UI가 붙으면 이 파일과 호출부를 전부 제거합니다.
/// </summary>
public static class MppmMatchRole
{
    // 두 인스턴스가 같은 머신에서 도는 것을 전제로 한 고정 경로입니다.
    // persistentDataPath는 클론과 겹칠 수 있어 OS 임시 폴더를 씁니다.
    private static readonly string JoinCodePath =
        Path.Combine(Path.GetTempPath(), "chaoschess_relay_joincode.txt");

    // 지난 판에서 남은 join code를 새 판의 것으로 오인하지 않기 위한 기준 시각입니다.
    //
    // static 필드는 클래스에 처음 접근할 때 초기화되므로, 그대로 두면 기준 시각이
    // "이 인스턴스가 MppmMatchRole을 처음 건드린 때"가 됩니다. 그러면 호스트가 게스트보다
    // 먼저 게임을 시작해 join code를 남긴 경우, 게스트 쪽 기준 시각이 파일보다 나중이라
    // **이번 판의 코드를 지난 판의 잔재로 오인해 영영 무시합니다.**
    // 3c에서 방을 여는 시점이 매치 씬에서 MainScene으로 당겨지면서 이 순서가 실제로 벌어집니다.
    //
    // 그래서 Play 시작 시점에 못박습니다. 두 인스턴스 모두 그때 기준이 잡히므로
    // 이후에 쓰인 파일은 항상 이번 판의 것입니다.
    private static readonly DateTime SessionStartUtc = DateTime.UtcNow;

    // 오래된 파일을 무시했다고 한 번만 알리기 위한 플래그입니다.
    private static bool staleFileWarned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CaptureSessionStart()
    {
        // 값을 읽는 것만으로 static 초기화가 일어납니다.
        if (SessionStartUtc == default)
            Debug.LogWarning("[MPPM] 세션 시작 시각을 잡지 못했습니다.");
    }

    /// <summary>에디터에서 도는 중이라 역할 자동 배정을 쓸 수 있는지 여부입니다.</summary>
    public static bool IsAvailable
    {
#if UNITY_EDITOR
        get => true;
#else
        get => false;
#endif
    }

    /// <summary>메인 에디터는 방을 열고, 클론은 들어갑니다.</summary>
    public static MatchRole Role
    {
#if UNITY_EDITOR
        get => CurrentPlayer.IsMainEditor ? MatchRole.Host : MatchRole.Guest;
#else
        get => MatchRole.Host;
#endif
    }

    /// <summary>역할에서 진영을 파생합니다. 호스트가 백을 잡습니다.</summary>
    public static PieceColor Color =>
        Role == MatchRole.Host ? PieceColor.White : PieceColor.Black;

    /// <summary>
    /// 지난 판에서 남은 join code 파일을 지웁니다.
    /// 호스트가 방을 열기 전에 불러야 게스트가 죽은 코드로 접속하지 않습니다.
    /// </summary>
    public static void ClearJoinCode()
    {
        try
        {
            if (File.Exists(JoinCodePath))
                File.Delete(JoinCodePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MPPM] join code 파일을 지우지 못했습니다: {e.Message}");
        }
    }

    /// <summary>호스트가 발급받은 join code를 게스트가 읽을 수 있도록 남깁니다.</summary>
    public static void PublishJoinCode(string joinCode)
    {
        try
        {
            File.WriteAllText(JoinCodePath, joinCode);
            Debug.Log($"[MPPM] join code를 게스트에게 전달했습니다: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MPPM] join code를 남기지 못했습니다: {e.Message}");
        }
    }

    /// <summary>
    /// 호스트가 남긴 join code를 읽습니다.
    /// 아직 방이 열리지 않았으면 false를 돌려주므로 호출측이 계속 시도하면 됩니다.
    /// </summary>
    public static bool TryReadJoinCode(out string joinCode)
    {
        joinCode = null;

        try
        {
            if (!File.Exists(JoinCodePath))
                return false;

            // 이번 Play 세션이 시작되기 전에 쓰인 파일이면 지난 판의 잔재입니다.
            if (File.GetLastWriteTimeUtc(JoinCodePath) < SessionStartUtc)
            {
                // 계속 무시하면 게스트가 영영 접속하지 못하므로 한 번은 드러냅니다.
                if (!staleFileWarned)
                {
                    staleFileWarned = true;
                    Debug.LogWarning(
                        "[MPPM] join code 파일이 이번 Play 세션보다 오래됐습니다. " +
                        "지난 판의 잔재로 보고 무시합니다.");
                }

                return false;
            }

            string text = File.ReadAllText(JoinCodePath).Trim();
            if (string.IsNullOrEmpty(text))
                return false;

            joinCode = text;
            return true;
        }
        catch (Exception)
        {
            // 호스트가 쓰는 중이면 읽기가 실패할 수 있습니다. 다음 프레임에 다시 시도합니다.
            return false;
        }
    }
}
