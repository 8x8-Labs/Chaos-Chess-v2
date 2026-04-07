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
    public void InitEngine(string variant = "chess")
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

            string variantFile = Path.Combine(Application.streamingAssetsPath, "variants.ini");
            SendCommand("setoption name VariantPath value " + variantFile);

            // 여기 중요
            SendCommand("setoption name UCI_Variant value chaoschess");

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
#if UNITY_ANDROID && !UNITY_EDITOR
    // Android: Java 쪽 엔진 API 호출
    if (_fairyInstance == null) return false;
    return _fairyInstance.Call<bool>("isInCheck");

#else
        // PC: UCI "d" 명령으로 체크 상태 파싱

        // 엔진 준비
        SendCommand("isready");
        WaitForOutput("readyok", 3000);

        // 현재 포지션 복구
        RestorePosition();

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
                    // UnityEngine.Debug.Log("[UCI ←] " + line);  // ← 메인 스레드에서 출력
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