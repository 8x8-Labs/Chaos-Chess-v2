using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections.Generic;

public class FairyStockfishBridge : MonoBehaviour
{
    private static FairyStockfishBridge _instance;
    public static FairyStockfishBridge Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<FairyStockfishBridge>();
            return _instance;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _fairyInstance;
#else
    private Process _process;
    private StreamWriter _input;
    private volatile bool _isThinking = false;
    private string _currentFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private string _currentMoves = "";

    // 비동기 출력 큐
    private Queue<string> _outputQueue = new Queue<string>();
    private object _queueLock = new object();
#endif

    void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 초기화 ──────────────────────────────────────────
    public void InitEngine(string variant = "chaos_base")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass javaClass =
                new AndroidJavaClass("com.example.chessaiv2.FairyStockfish");
            _fairyInstance = javaClass.CallStatic<AndroidJavaObject>("getInstance");
            _fairyInstance.Call("initialize", variant);
            UnityEngine.Debug.Log("[Fairy] Android 초기화 성공");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[Fairy] Android 초기화 실패: " + e.Message);
        }
#else
        try
        {
            string exePath = Path.Combine(
                Application.streamingAssetsPath,
                "fairy-stockfish.exe");

            if (!File.Exists(exePath))
            {
                UnityEngine.Debug.LogError("[Fairy] 실행 파일 없음: " + exePath);
                return;
            }

            _process = new Process();
            _process.StartInfo.FileName = exePath;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.CreateNoWindow = true;
            _process.Start();
            _input = _process.StandardInput;

            // 비동기 출력 읽기 시작
            StartReadingOutput();

            SendCommand("uci");
            SendCommand("setoption name UCI_Variant value " + variant);
            SendCommand("isready");
            WaitForOutput("readyok");

            UnityEngine.Debug.Log("[Fairy] PC 프로세스 초기화 성공");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[Fairy] PC 초기화 실패: " + e.Message);
        }
#endif
    }

    // ── 포지션 설정 ──────────────────────────────────────
    public void SetPosition(string fen, string moves = "")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _fairyInstance?.Call("setPosition", fen, moves);
#else
        _currentFen = fen;
        _currentMoves = moves;

        string cmd = "position fen " + fen;
        if (!string.IsNullOrEmpty(moves))
            cmd += " moves " + moves;
        SendCommand(cmd);
#endif
    }

    // ── 포지션 복구 (내부용) ─────────────────────────────
#if !UNITY_ANDROID || UNITY_EDITOR
    private void RestorePosition()
    {
        string cmd = "position fen " + _currentFen;
        if (!string.IsNullOrEmpty(_currentMoves))
            cmd += " moves " + _currentMoves;
        SendCommand(cmd);
    }
#endif

    // ── 최선의 수 (비동기) ───────────────────────────────
    public void GetBestMoveAsync(int depth, int moveTimeMs, Action<string> callback)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaProxy proxy = new BestMoveProxy(callback);
        _fairyInstance?.Call("getBestMoveAsync", depth, moveTimeMs, proxy);
#else
        _isThinking = true;
        string command = depth > 0
            ? "go depth " + depth
            : "go movetime " + moveTimeMs;

        Thread thread = new Thread(() =>
        {
            SendCommand(command);
            string bestMove = WaitForBestMove();
            _isThinking = false;
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                callback?.Invoke(bestMove);
            });
        });
        thread.Start();
#endif
    }

    // ── 합법적인 수 전체 반환 ────────────────────────────
    public string[] GetLegalMoves()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_fairyInstance == null) return new string[0];
        string movesStr = _fairyInstance.Call<string>("getLegalMoves");
        return string.IsNullOrEmpty(movesStr)
            ? new string[0] : movesStr.Split(' ');
#else
        // 엔진 준비 확인 후 포지션 재설정
        SendCommand("isready");
        WaitForOutput("readyok", 3000);
        RestorePosition();

        // perft 실행
        SendCommand("go perft 1");
        string output = WaitForOutput("Nodes searched", 8000);

        UnityEngine.Debug.Log("[Fairy] perft output:\n" + output);

        // 포지션 복구
        RestorePosition();

        var moves = new List<string>();
        foreach (string line in output.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Contains(": ") && !trimmed.StartsWith("Nodes"))
            {
                string move = trimmed.Split(':')[0].Trim();
                if (!string.IsNullOrEmpty(move) && move.Length >= 4)
                    moves.Add(move);
            }
        }

        UnityEngine.Debug.Log("[Fairy] 합법적인 수: " + moves.Count + "개");
        return moves.ToArray();
#endif
    }

    // ── 특정 칸에서 이동 가능한 수 반환 ─────────────────
    public string[] GetLegalMovesFromSquare(string square)
    {
        string[] allMoves = GetLegalMoves();
        var filtered = new List<string>();
        foreach (string move in allMoves)
        {
            if (move.Length >= 4 && move.Substring(0, 2) == square)
                filtered.Add(move);
        }
        return filtered.ToArray();
    }

    // ── 게임 결과 확인 ───────────────────────────────────
    public int GetGameResult()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return _fairyInstance?.Call<int>("getGameResult") ?? 1;
#else
        string[] moves = GetLegalMoves();
        if (moves.Length == 0) return -1;
        return 1;
#endif
    }

    // ── UCI 헬퍼 함수 (PC 전용) ──────────────────────────
#if !UNITY_ANDROID || UNITY_EDITOR
    private void StartReadingOutput()
    {
        Thread readThread = new Thread(() =>
        {
            while (_process != null && !_process.HasExited)
            {
                string line = _process.StandardOutput.ReadLine();
                if (line != null)
                {
                    lock (_queueLock)
                    {
                        _outputQueue.Enqueue(line);
                    }
                    UnityEngine.Debug.Log("[UCI ←] " + line);
                }
            }
        });
        readThread.IsBackground = true;
        readThread.Start();
    }

    private void SendCommand(string command)
    {
        if (_process == null || _process.HasExited) return;
        _input.WriteLine(command);
        _input.Flush();
        UnityEngine.Debug.Log("[UCI →] " + command);
    }

    private string WaitForOutput(string keyword, int timeoutMs = 5000)
    {
        string result = "";
        DateTime timeout = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < timeout)
        {
            lock (_queueLock)
            {
                while (_outputQueue.Count > 0)
                {
                    string line = _outputQueue.Dequeue();
                    UnityEngine.Debug.Log("[UCI ←] " + line);  // ← 메인 스레드에서 출력
                    result += line + "\n";
                    if (line.Contains(keyword))
                        return result;
                }
            }
            Thread.Sleep(10);
        }
        return result;
    }

    private string WaitForBestMove(int timeoutMs = 10000)
    {
        DateTime timeout = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < timeout)
        {
            lock (_queueLock)
            {
                while (_outputQueue.Count > 0)
                {
                    string line = _outputQueue.Dequeue();
                    if (line.StartsWith("bestmove"))
                    {
                        string[] parts = line.Split(' ');
                        return parts.Length > 1 ? parts[1] : "none";
                    }
                }
            }
            Thread.Sleep(10);
        }
        return "none";
    }
#endif

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _fairyInstance?.Call("destroy");
        _fairyInstance?.Dispose();
#else
        if (_process != null && !_process.HasExited)
        {
            SendCommand("quit");
            _process.WaitForExit(1000);
            _process.Kill();
            _process.Dispose();
        }
#endif
    }

    // ── Android 콜백 프록시 ──────────────────────────────
    private class BestMoveProxy : AndroidJavaProxy
    {
        private Action<string> _callback;
        public BestMoveProxy(Action<string> callback)
            : base("com.example.chessaiv2.FairyStockfish$BestMoveCallback")
        {
            _callback = callback;
        }
        public void onBestMove(string move)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _callback?.Invoke(move);
            });
        }
    }
}