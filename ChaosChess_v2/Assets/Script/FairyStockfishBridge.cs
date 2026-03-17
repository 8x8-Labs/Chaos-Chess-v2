using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;


public class FairyStockfishBridge : MonoBehaviour
{
    // ── 이벤트 ───────────────────────────────────────
    public event Action<string> OnBestMove;
    public event Action<string> OnInfo;
    public event Action OnReady;
    public event Action<string> OnError;


    // ── 내부 상태 ─────────────────────────────────────
    private Process _process;
    private StreamWriter _writer;
    private StreamReader _reader;
    private Thread _readThread;
    private bool _isReady = false;
    private bool _isSearching = false;


    // ── 설정 ─────────────────────────────────────────
    [Header("Engine Settings")]
    [SerializeField] private string defaultVariant = "chess";
    [SerializeField] private int defaultDepth = 15;
    [SerializeField] private int defaultTimeMsec = 1000;


    void Awake()
    {
        StartCoroutine(InitEngineAsync());
    }


    // ── 초기화 ───────────────────────────────────────
    private IEnumerator InitEngineAsync()
    {
        string enginePath = GetEnginePath();
        string variantPath = Application.persistentDataPath + "/variants.ini";


        // 1. StreamingAssets → persistentDataPath 복사
        yield return CopyStreamingAsset("variants.ini", variantPath);


#if UNITY_ANDROID && !UNITY_EDITOR
        // 2. 실행 권한 부여
        SetExecutable(enginePath);
#endif


        // 3. 프로세스 시작
        LaunchProcess(enginePath, variantPath);
    }


    private string GetEnginePath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // nativeLibraryDir에 자동 추출됨
        return Application.persistentDataPath
               .Replace("/files", "/lib")  // 일반적인 경로 패턴
               + "/libfairystockfish.so";
        // 또는: AndroidJNI를 통해 applicationInfo.nativeLibraryDir 직접 획득
#else
        return Application.streamingAssetsPath + "/fairy-stockfish";
#endif
    }


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


            // 비동기 출력 읽기 스레드 시작
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


    // ── 출력 읽기 루프 (별도 스레드) ─────────────────
    private void ReadLoop()
    {
        string line;
        while ((line = _reader.ReadLine()) != null)
        {
            string captured = line;
            if (line == "readyok")
            {
                _isReady = true;
                // 기본 변형 설정
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


    // ── 공개 API ─────────────────────────────────────
    public void SetVariant(string variantName)
    {
        SendCommand($"setoption name UCI_Variant value {variantName}");
        SendCommand("ucinewgame");
    }


    public void SetOption(string name, string value)
    {
        SendCommand($"setoption name {name} value {value}");
    }


    public void RequestBestMove(string fen, int depth = -1)
    {
        if (!_isReady || _isSearching) return;
        _isSearching = true;
        SendCommand($"position fen {fen}");
        int d = depth > 0 ? depth : defaultDepth;
        SendCommand($"go depth {d}");
    }


    public void RequestBestMoveByTime(string fen, int msec = -1)
    {
        if (!_isReady || _isSearching) return;
        _isSearching = true;
        SendCommand($"position fen {fen}");
        int t = msec > 0 ? msec : defaultTimeMsec;
        SendCommand($"go movetime {t}");
    }


    // 수 목록을 이어붙여 포지션 설정 (history 유지)
    public void RequestBestMoveFromMoves(string[] moves, int msec = -1)
    {
        if (!_isReady || _isSearching) return;
        _isSearching = true;
        string moveStr = string.Join(" ", moves);
        SendCommand($"position startpos moves {moveStr}");
        int t = msec > 0 ? msec : defaultTimeMsec;
        SendCommand($"go movetime {t}");
    }


    public void StopSearch()
    {
        if (_isSearching)
            SendCommand("stop");
    }


    public bool IsReady => _isReady;
    public bool IsSearching => _isSearching;


    // ── 내부 ─────────────────────────────────────────
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
        if (File.Exists(destPath)) { yield break; }


        string srcUrl = Path.Combine(Application.streamingAssetsPath, filename);
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android StreamingAssets는 jar 내부이므로 UnityWebRequest 필요
        using var req = UnityWebRequest.Get(srcUrl);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
            File.WriteAllBytes(destPath, req.downloadHandler.data);
#else
        File.Copy(srcUrl, destPath, true);
        yield return null;
#endif
    }


    private void SetExecutable(string path)
    {
        try
        {
            var chmod = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/system/bin/chmod",
                    Arguments = $"755 \"{path}\"",
                    UseShellExecute = false,
                }
            };
            chmod.Start();
            chmod.WaitForExit();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Fairy] chmod 실패: {e.Message}");
        }
    }


    void OnDestroy()
    {
        SendCommand("quit");
        _process?.Kill();
        _process?.Dispose();
    }
}
