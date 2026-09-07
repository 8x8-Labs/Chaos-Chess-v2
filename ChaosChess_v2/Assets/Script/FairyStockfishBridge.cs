using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using ChaosChess.Unity.AIIntegration.Engine;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

public class FairyStockfishBridge : MonoBehaviour
{
    private static FairyStockfishBridge _instance;
    private int _analysisRequestSequence = 0;
    private string _currentFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private string _currentMoves = "";

    // 이미 초기화를 마친 변형입니다. 같은 변형으로 다시 부르면 엔진을 재기동하지 않습니다.
    private string _initializedVariant;
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
    private volatile bool _isAnalyzing = false;
    private volatile bool _isGettingLegalMoves = false;

    // 비동기 출력 큐
    private Queue<string> _outputQueue = new Queue<string>();
    private object _queueLock = new object();
#endif

    public bool IsBusy
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return false;
#else
            return _isThinking || _isAnalyzing || _isGettingLegalMoves;
#endif
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 초기화 ──────────────────────────────────────────
    /// <summary>
    /// 엔진을 띄웁니다. 이미 같은 변형으로 살아 있으면 그대로 재사용합니다.
    ///
    /// 매치마다 다시 띄우면 이전 프로세스가 정리되지 않고 남을 뿐 아니라, 프로세스 기동과
    /// readyok 대기가 **메인 스레드를 수 초간 멈춥니다.** 원격 대전에서는 그 사이 Relay
    /// 드라이버가 돌지 못해 "player timed out due to inactivity"로 연결이 끊깁니다.
    /// </summary>
    public void InitEngine(string variant = "chess")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_fairyInstance != null && _initializedVariant == variant)
            return;

        try
        {
            AndroidJavaClass javaClass =
                new AndroidJavaClass("com.example.chessaiv2.FairyStockfish");
            _fairyInstance = javaClass.CallStatic<AndroidJavaObject>("getInstance");
            _fairyInstance.Call("initialize", variant);
            _initializedVariant = variant;
            UnityEngine.Debug.Log("[Fairy] Android 초기화 성공");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[Fairy] Android 초기화 실패: " + e.Message);
        }
#else
        if (_process != null && !_process.HasExited && _initializedVariant == variant)
            return;

        // 변형이 바뀌었거나 프로세스가 죽었으면 남은 것을 정리하고 다시 띄웁니다.
        ShutdownProcess();

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

            string variantFile = Path.Combine(Application.streamingAssetsPath, "variants.ini");
            SendCommand("setoption name VariantPath value " + variantFile);

            // 여기 중요
            SendCommand("setoption name UCI_Variant value chaoschess");

            SendCommand("isready");
            WaitForOutput("readyok");

            _initializedVariant = variant;
            UnityEngine.Debug.Log("[Fairy] PC 프로세스 초기화 성공");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[Fairy] PC 초기화 실패: " + e.Message);
        }
#endif
    }

#if !UNITY_ANDROID || UNITY_EDITOR
    /// <summary>엔진 프로세스를 정리합니다. 이미 없으면 아무것도 하지 않습니다.</summary>
    private void ShutdownProcess()
    {
        if (_process == null) return;

        try
        {
            if (!_process.HasExited)
            {
                SendCommand("quit");
                _process.WaitForExit(1000);

                if (!_process.HasExited)
                    _process.Kill();
            }

            _process.Dispose();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[Fairy] 엔진 종료 중 오류: " + e.Message);
        }

        // 읽기 스레드는 _process가 null이 되면 루프를 빠져나갑니다.
        _process = null;
        _input = null;
        _initializedVariant = null;

        lock (_queueLock)
        {
            _outputQueue.Clear();
        }
    }
#endif

    // ── ELO 강도 설정 ────────────────────────────────────
    public void SetElo(int elo)
    {
        elo = Mathf.Clamp(elo, 500, 2850);
#if UNITY_ANDROID && !UNITY_EDITOR
        _fairyInstance?.Call("setElo", elo);
#else
        SendCommand("setoption name UCI_LimitStrength value true");
        SendCommand("setoption name UCI_Elo value " + elo);
#endif
    }

    // ── 포지션 설정 ──────────────────────────────────────
    public void SetPosition(string fen, string moves = "")
    {
        _currentFen = fen;
        _currentMoves = moves ?? "";
        ApplyPosition(_currentFen, _currentMoves);
    }

    // ── 포지션 전송 (상태 저장 없이 엔진에만 반영) ─────────────────
    private void ApplyPosition(string fen, string moves = "")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _fairyInstance?.Call("setPosition", fen, moves);
#else
        string cmd = "position fen " + fen;
        if (!string.IsNullOrEmpty(moves))
            cmd += " moves " + moves;
        SendCommand(cmd);
#endif
    }

    // ── 포지션 복구 (내부용) ─────────────────────────────
    private void RestorePosition()
    {
        ApplyPosition(_currentFen, _currentMoves);
    }

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
            lock (_queueLock)
            {
                _outputQueue.Clear();
            }

            SendCommand(command);
            int timeoutMs = moveTimeMs > 0 ? Mathf.Max(moveTimeMs + 1000, 3000) : 10000;
            string bestMove = WaitForBestMove(timeoutMs);
            _isThinking = false;
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                callback?.Invoke(bestMove);
            });
        });
        thread.Start();
#endif
    }

    public void AnalyzePositionAsync(
        string fen,
        int depth,
        int variationCount,
        AiPieceColor perspective,
        Action<int, UciAnalysisSnapshot> onComplete,
        Action<int, string> onError = null)
    {
        int requestId = Interlocked.Increment(ref _analysisRequestSequence);

#if UNITY_ANDROID && !UNITY_EDITOR
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            onError?.Invoke(requestId, "MultiPV analysis is not supported on Android yet.");
        });
#else
        if (string.IsNullOrWhiteSpace(fen))
        {
            EnqueueAnalysisError(requestId, "FEN cannot be empty.", onError);
            return;
        }

        if (depth <= 0)
        {
            EnqueueAnalysisError(requestId, "Analysis depth must be positive.", onError);
            return;
        }

        if (variationCount <= 0)
        {
            EnqueueAnalysisError(requestId, "Variation count must be positive.", onError);
            return;
        }

        if (_isAnalyzing || _isThinking)
        {
            EnqueueAnalysisError(requestId, "Fairy Stockfish is already handling another request.", onError);
            return;
        }

        _isAnalyzing = true;

        Thread thread = new Thread(() =>
        {
            UciAnalysisSnapshot snapshot = null;
            string error = null;

            try
            {
                lock (_queueLock)
                {
                    _outputQueue.Clear();
                }

                SendCommand("setoption name MultiPV value " + variationCount);
                SendCommand("position fen " + fen);
                SendCommand("go depth " + depth);

                int timeoutMs = Mathf.Max(depth * 1000, 10000);
                List<string> output = WaitForAnalysisOutput(timeoutMs, out bool sawBestMove);

                if (!sawBestMove)
                {
                    error = "Analysis timed out before bestmove.";
                }
                else
                {
                    snapshot = UciMultiPvParser.ParseLines(output, variationCount, perspective);
                }
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            finally
            {
                SendCommand("setoption name MultiPV value 1");
                RestorePosition();
                _isAnalyzing = false;
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (this == null)
                    return;

                if (snapshot != null)
                    onComplete?.Invoke(requestId, snapshot);
                else
                    onError?.Invoke(requestId, error ?? "Analysis failed.");
            });
        });

        thread.IsBackground = true;
        thread.Start();
#endif
    }

    // ── 합법적인 수 전체 비동기 반환 ─────────────────────
    public void GetLegalMovesAsync(Action<string[]> callback)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        string[] moves = GetLegalMoves();
        callback?.Invoke(moves);
#else
        _isGettingLegalMoves = true;
        Thread thread = new Thread(() =>
        {
            string[] moves = Array.Empty<string>();
            string error = null;
            try
            {
                moves = GetLegalMoves();
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            finally
            {
                _isGettingLegalMoves = false;
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (!string.IsNullOrEmpty(error))
                    UnityEngine.Debug.LogError("[Fairy] GetLegalMovesAsync failed: " + error);

                callback?.Invoke(moves);
            });
        });
        thread.IsBackground = true;
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

        //        UnityEngine.Debug.Log("[Fairy] perft output:\n" + output);

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

        // UnityEngine.Debug.Log("[Fairy] 합법적인 수: " + moves.Count + "개");
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

    // ── 체크 확인 ─────────────────
    public bool IsInCheck()
    {
        return IsInCheck(_currentFen, _currentMoves);
    }

    public bool IsInCheck(string fen)
    {
        return IsInCheck(fen, "");
    }

    private bool IsInCheck(string fen, string moves)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: Java 쪽 엔진 API 호출
        if (_fairyInstance == null) return false;

        string previousFen = _currentFen;
        string previousMoves = _currentMoves;
        try
        {
            _fairyInstance.Call("setPosition", fen, moves ?? "");
            return _fairyInstance.Call<bool>("isInCheck");
        }
        finally
        {
            _fairyInstance.Call("setPosition", previousFen, previousMoves ?? "");
        }

#else
        // PC: UCI "d" 명령으로 체크 상태 파싱

        // 엔진 준비
        SendCommand("isready");
        WaitForOutput("readyok", 3000);

        // 요청한 포지션 설정
        ApplyPosition(fen, moves);

        // 디버그 정보 요청 (Checkers 포함)
        SendCommand("d");

        string output = WaitForOutput("Checkers:", 3000);

        // 포지션 다시 복구
        RestorePosition();

        // "Checkers:" 라인 파싱
        foreach (string line in output.Split('\n'))
        {
            if (line.StartsWith("Checkers:"))
            {
                string data = line.Substring("Checkers:".Length).Trim();
                return !string.IsNullOrEmpty(data);
            }
        }

        return false;
#endif
    }

    // ── 기물 부족 무승부 확인 ────────────────────────────
    public bool IsInsufficientMaterial()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_fairyInstance == null) return false;
        return _fairyInstance.Call<bool>("isInsufficientMaterial");
#else
        // PC: 현재 FEN에서 기물을 파싱해 기물 부족 판별
        // 기물 부족 조건 (표준 체스 기준):
        //   K vs K
        //   K+B vs K  /  K+N vs K
        //   K+B vs K+B (같은 색 비숍)
        string piecePart = _currentFen.Split(' ')[0];

        int whiteBishops = 0, whiteKnights = 0, whiteOther = 0;
        int blackBishops = 0, blackKnights = 0, blackOther = 0;

        // 비숍 색상 판별용 (파일+랭크 합의 홀짝)
        int lastWhiteBishopColor = -1, lastBlackBishopColor = -1;
        bool bishopColorMismatch = false;

        int file = 0, rank = 7;
        foreach (char c in piecePart)
        {
            if (c == '/') { rank--; file = 0; continue; }
            if (char.IsDigit(c)) { file += c - '0'; continue; }

            char lower = char.ToLower(c);
            bool isWhite = char.IsUpper(c);
            int squareColor = (file + rank) % 2;

            switch (lower)
            {
                case 'k': break; // 킹은 항상 존재
                case 'b':
                    if (isWhite)
                    {
                        if (lastWhiteBishopColor == -1) lastWhiteBishopColor = squareColor;
                        else if (lastWhiteBishopColor != squareColor) bishopColorMismatch = true;
                        whiteBishops++;
                    }
                    else
                    {
                        if (lastBlackBishopColor == -1) lastBlackBishopColor = squareColor;
                        else if (lastBlackBishopColor != squareColor) bishopColorMismatch = true;
                        blackBishops++;
                    }
                    break;
                case 'n':
                    if (isWhite) whiteKnights++; else blackKnights++;
                    break;
                default:
                    if (lower != 'k')
                    {
                        if (isWhite) whiteOther++; else blackOther++;
                    }
                    break;
            }
            file++;
        }

        // 폰/루크/퀸 등 전력 기물이 있으면 무승부 아님
        if (whiteOther > 0 || blackOther > 0) return false;

        int whitePieces = whiteBishops + whiteKnights;
        int blackPieces = blackBishops + blackKnights;

        // K vs K
        if (whitePieces == 0 && blackPieces == 0) return true;

        // K+B vs K  /  K+N vs K
        if (whitePieces == 0 && blackPieces == 1) return true;
        if (blackPieces == 0 && whitePieces == 1) return true;

        // K+B vs K+B (비숍이 각각 1개씩, 같은 색 칸)
        if (whiteBishops == 1 && whiteKnights == 0 &&
            blackBishops == 1 && blackKnights == 0 &&
            !bishopColorMismatch &&
            lastWhiteBishopColor == lastBlackBishopColor)
            return true;

        return false;
#endif
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
                    //                    UnityEngine.Debug.Log("[UCI ←] " + line);
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
        //        UnityEngine.Debug.Log("[UCI →] " + command);
    }

    // 이 시간을 넘게 기다렸으면 알립니다. 메인 스레드에서 불리는 경로가 있어
    // 여기서 멈춘 만큼 Relay 드라이버도 함께 멈춥니다.
    private const double SlowOutputWarningMs = 500;

    private string WaitForOutput(string keyword, int timeoutMs = 5000)
    {
        string result = "";
        DateTime start = DateTime.Now;
        DateTime timeout = start.AddMilliseconds(timeoutMs);

        while (DateTime.Now < timeout)
        {
            lock (_queueLock)
            {
                while (_outputQueue.Count > 0)
                {
                    string line = _outputQueue.Dequeue();
                    // UnityEngine.Debug.Log("[UCI ←] " + line);  // ← 메인 스레드에서 출력
                    result += line + "\n";
                    if (line.Contains(keyword))
                    {
                        WarnIfSlow(keyword, start);
                        return result;
                    }
                }
            }
            Thread.Sleep(10);
        }

        UnityEngine.Debug.LogWarning(
            $"[Fairy] '{keyword}'를 {timeoutMs}ms 안에 받지 못했습니다. " +
            "그 시간만큼 호출한 스레드가 멈춥니다.");
        return result;
    }

    /// <summary>엔진 응답이 느렸으면 얼마나 기다렸는지 남깁니다.</summary>
    private static void WarnIfSlow(string keyword, DateTime start)
    {
        double elapsed = (DateTime.Now - start).TotalMilliseconds;
        if (elapsed < SlowOutputWarningMs) return;

        UnityEngine.Debug.LogWarning($"[Fairy] '{keyword}' 대기에 {elapsed:F0}ms 걸렸습니다.");
    }

    private string WaitForBestMove(int timeoutMs = 10000)
    {
        DateTime timeout = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < timeout)
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

        SendCommand("stop");
        timeout = DateTime.UtcNow.AddMilliseconds(1000);
        while (DateTime.UtcNow < timeout)
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

    private List<string> WaitForAnalysisOutput(int timeoutMs, out bool sawBestMove)
    {
        var result = new List<string>();
        sawBestMove = false;

        DateTime timeout = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < timeout)
        {
            if (DrainAnalysisOutput(result, out sawBestMove))
                return result;

            Thread.Sleep(10);
        }

        SendCommand("stop");

        timeout = DateTime.UtcNow.AddMilliseconds(1000);
        while (DateTime.UtcNow < timeout)
        {
            if (DrainAnalysisOutput(result, out sawBestMove))
                return result;

            Thread.Sleep(10);
        }

        return result;
    }

    private bool DrainAnalysisOutput(List<string> result, out bool sawBestMove)
    {
        sawBestMove = false;

        lock (_queueLock)
        {
            while (_outputQueue.Count > 0)
            {
                string line = _outputQueue.Dequeue();
                result.Add(line);
                if (line.StartsWith("bestmove"))
                {
                    sawBestMove = true;
                    return true;
                }
            }
        }

        return false;
    }

    private void EnqueueAnalysisError(int requestId, string message, Action<int, string> onError)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            onError?.Invoke(requestId, message);
        });
    }
#endif

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _fairyInstance?.Call("destroy");
        _fairyInstance?.Dispose();
#else
        ShutdownProcess();
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
