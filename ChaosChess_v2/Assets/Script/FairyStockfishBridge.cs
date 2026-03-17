using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public class FairyStockfishBridge : MonoBehaviour
{
    // ── 이벤트 ───────────────────────────────────────────────────
    public event Action<string> OnBestMove;
    public event Action<string> OnInfo;
    public event Action OnReady;
    public event Action<string> OnError;

    // ── 내부 상태 ────────────────────────────────────────────────
    private Process _process;
    private StreamWriter _writer;
    private StreamReader _reader;
    private Thread _readThread;
    private bool _isReady = false;
    private bool _isSearching = false;
    private string _enginePath = "";

    // ── Inspector 설정 ───────────────────────────────────────────
    [Header("Engine Settings")]
    [SerializeField] private string defaultVariant = "chess";
    [SerializeField] private int defaultDepth = 15;
    [SerializeField] private int defaultTimeMsec = 1000;

    // ────────────────────────────────────────────────────────────
    void Awake()
    {
        StartCoroutine(InitEngineAsync());
    }

    // ── 초기화 ───────────────────────────────────────────────────
    private IEnumerator InitEngineAsync()
    {
        // ✅ persistentDataPath 대신 내부 저장소 직접 사용
        string internalPath = GetInternalPath();
        string variantPath = internalPath + "/variants.ini";

        yield return StartCoroutine(CopyStreamingAsset("variants.ini", variantPath));

#if UNITY_ANDROID && !UNITY_EDITOR
    yield return StartCoroutine(PrepareEngine());
#else
        _enginePath = GetEditorEnginePath();
#endif

        if (string.IsNullOrEmpty(_enginePath))
        {
            UnityEngine.Debug.LogError("[Fairy] 엔진 경로 설정 실패");
            yield break;
        }

        LaunchProcess(_enginePath, variantPath);
    }

    // 내부 저장소 경로 획득 헬퍼
    private string GetInternalPath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var filesDir    = activity.Call<AndroidJavaObject>("getFilesDir");
        return filesDir.Call<string>("getAbsolutePath");
    }
    catch
    {
        return Application.persistentDataPath;
    }
#else
        return Application.persistentDataPath;
#endif
    }

    // ── Android: .so → persistentDataPath 복사 후 실행 ──────────
#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator PrepareEngine()
    {
        string nativeLibDir;
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var appInfo     = activity.Call<AndroidJavaObject>("getApplicationInfo");
            nativeLibDir          = appInfo.Get<string>("nativeLibraryDir");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[Fairy] nativeLibraryDir 획득 실패: {e.Message}");
            OnError?.Invoke(e.Message);
            yield break;
        }

        // ✅ 핵심: 내부 저장소 경로 직접 획득 (/data/data/패키지명/files/)
        string internalPath;
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var filesDir    = activity.Call<AndroidJavaObject>("getFilesDir");
            internalPath          = filesDir.Call<string>("getAbsolutePath");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[Fairy] 내부 경로 획득 실패: {e.Message}");
            OnError?.Invoke(e.Message);
            yield break;
        }

        string srcPath  = nativeLibDir + "/libChessAI.so";
        string destPath = internalPath + "/ChessAI";   // ✅ /data/data/.../files/ChessAI

        UnityEngine.Debug.Log($"[Fairy] 내부 경로: {destPath}");

        if (!File.Exists(destPath))
        {
            try
            {
                File.Copy(srcPath, destPath, true);
                UnityEngine.Debug.Log($"[Fairy] 복사 완료");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Fairy] 복사 실패: {e.Message}");
                OnError?.Invoke(e.Message);
                yield break;
            }
        }

        // chmod
        try
        {
            var chmod = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName        = "/system/bin/chmod",
                    Arguments       = $"700 \"{destPath}\"",
                    UseShellExecute = false,
                }
            };
            chmod.Start();
            chmod.WaitForExit();
            UnityEngine.Debug.Log($"[Fairy] chmod 완료");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Fairy] chmod 실패: {e.Message}");
        }

        _enginePath = destPath;
        UnityEngine.Debug.Log($"[Fairy] 엔진 준비 완료: {_enginePath}");
        yield return null;
    }
#endif

    // ── Windows 에디터용 경로 ────────────────────────────────────
    private string GetEditorEnginePath()
    {
#if UNITY_EDITOR_WIN
        return Application.streamingAssetsPath + "/fairy-stockfish.exe";
#else
        return Application.streamingAssetsPath + "/fairy-stockfish";
#endif
    }

    // ── 프로세스 시작 ────────────────────────────────────────────
    private void LaunchProcess(string enginePath, string variantPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = enginePath,
                Arguments = $"load \"{variantPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _process = new Process { StartInfo = psi };
            _process.Start();

            _writer = _process.StandardInput;
            _reader = _process.StandardOutput;

            // 비동기 출력 읽기 스레드
            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();

            // UCI 핸드셰이크
            SendCommand("uci");
            SendCommand("isready");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[Fairy] 엔진 시작 실패: {e.Message}");
            OnError?.Invoke(e.Message);
        }
    }

    // ── 출력 읽기 루프 (별도 스레드) ────────────────────────────
    private void ReadLoop()
    {
        string line;
        while ((line = _reader.ReadLine()) != null)
        {
            string captured = line;
            if (line == "readyok")
            {
                _isReady = true;
                SendCommand($"setoption name UCI_Variant value {defaultVariant}");
                SendCommand("ucinewgame");
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnReady?.Invoke());
            }
            else if (line.StartsWith("bestmove"))
            {
                _isSearching = false;
                string[] parts = line.Split(' ');
                string move = parts.Length > 1 ? parts[1] : "none";
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnBestMove?.Invoke(move));
            }
            else if (line.StartsWith("info"))
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnInfo?.Invoke(captured));
            }
        }
    }

    // ── 공개 API ─────────────────────────────────────────────────
    public void SetVariant(string variantName)
    {
        SendCommand($"setoption name UCI_Variant value {variantName}");
        SendCommand("ucinewgame");
    }

    public void SetOption(string name, string value)
    {
        SendCommand($"setoption name {name} value {value}");
    }

    /// <summary>FEN 포지션에서 depth 기준으로 최선수 요청</summary>
    public void RequestBestMove(string fen, int depth = -1)
    {
        if (!_isReady || _isSearching) return;
        _isSearching = true;
        SendCommand($"position fen {fen}");
        SendCommand($"go depth {(depth > 0 ? depth : defaultDepth)}");
    }

    /// <summary>FEN 포지션에서 시간 기준으로 최선수 요청</summary>
    public void RequestBestMoveByTime(string fen, int msec = -1)
    {
        if (!_isReady || _isSearching) return;
        _isSearching = true;
        SendCommand($"position fen {fen}");
        SendCommand($"go movetime {(msec > 0 ? msec : defaultTimeMsec)}");
    }

    /// <summary>수 목록(history)으로 포지션 설정 후 최선수 요청</summary>
    public void RequestBestMoveFromMoves(string[] moves, int msec = -1)
    {
        if (!_isReady || _isSearching) return;
        _isSearching = true;
        string moveStr = string.Join(" ", moves);
        SendCommand($"position startpos moves {moveStr}");
        SendCommand($"go movetime {(msec > 0 ? msec : defaultTimeMsec)}");
    }

    public void StopSearch()
    {
        if (_isSearching) SendCommand("stop");
    }

    public bool IsReady => _isReady;
    public bool IsSearching => _isSearching;

    // ── 내부 유틸 ────────────────────────────────────────────────
    private void SendCommand(string cmd)
    {
        try
        {
            _writer?.WriteLine(cmd);
            _writer?.Flush();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Fairy] 명령 전송 실패: {e.Message}");
        }
    }

    private IEnumerator CopyStreamingAsset(string filename, string destPath)
    {
        if (File.Exists(destPath)) yield break;

        string srcUrl = Path.Combine(Application.streamingAssetsPath, filename);

#if UNITY_ANDROID && !UNITY_EDITOR
        using var req = UnityWebRequest.Get(srcUrl);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
            File.WriteAllBytes(destPath, req.downloadHandler.data);
        else
            UnityEngine.Debug.LogError($"[Fairy] variants.ini 복사 실패: {req.error}");
#else
        File.Copy(srcUrl, destPath, true);
        yield return null;
#endif
    }

    void OnDestroy()
    {
        SendCommand("quit");
        _process?.Kill();
        _process?.Dispose();
        _readThread = null;
    }
}