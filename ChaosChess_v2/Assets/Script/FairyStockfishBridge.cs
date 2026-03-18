using UnityEngine;
using System;

public class FairyStockfishBridge : MonoBehaviour
{
    private AndroidJavaObject _fairyInstance;
    private static FairyStockfishBridge _instance;

    public static FairyStockfishBridge Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<FairyStockfishBridge>();
            return _instance;
        }
    }

    void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitEngine();
    }

    public void InitEngine(string variant = "chess")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass javaClass =
                new AndroidJavaClass("com.example.chessaiv2.FairyStockfish");
            _fairyInstance = javaClass.CallStatic<AndroidJavaObject>("getInstance");
            _fairyInstance.Call("initialize", variant);
            Debug.Log("[Fairy] 초기화 성공: " + variant);
        }
        catch (Exception e)
        {
            Debug.LogError("[Fairy] 초기화 실패: " + e.Message);
        }
#else
        Debug.Log("[Fairy] 에디터 모드 - Android에서만 동작합니다.");
#endif
    }

    public void SetPosition(string fen, string moves = "")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _fairyInstance?.Call("setPosition", fen, moves);
#endif
    }

    public string GetBestMove(int depth = 10, int moveTimeMs = 2000)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_fairyInstance == null) return "none";
        return _fairyInstance.Call<string>("getBestMove", depth, moveTimeMs);
#else
        return "e2e4"; // 에디터 테스트용 더미
#endif
    }

    public void GetBestMoveAsync(int depth, int moveTimeMs, Action<string> callback)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaProxy proxy = new BestMoveProxy(callback);
        _fairyInstance?.Call("getBestMoveAsync", depth, moveTimeMs, proxy);
#else
        // 에디터에서는 코루틴으로 딜레이 시뮬레이션
        StartCoroutine(EditorBestMoveSimulation(callback));
#endif
    }

    private System.Collections.IEnumerator EditorBestMoveSimulation(Action<string> callback)
    {
        yield return new WaitForSeconds(0.5f);
        string[] dummyMoves = { "e2e4", "d2d4", "g1f3", "c2c4" };
        callback?.Invoke(dummyMoves[UnityEngine.Random.Range(0, dummyMoves.Length)]);
    }

    public string[] GetLegalMoves()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_fairyInstance == null) return new string[0];
        string movesStr = _fairyInstance.Call<string>("getLegalMoves");
        return string.IsNullOrEmpty(movesStr) ? new string[0] : movesStr.Split(' ');
#else
        return new string[] { "e2e4", "d2d4", "g1f3", "c2c4", "e7e5" };
#endif
    }

    public int GetGameResult()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return _fairyInstance?.Call<int>("getGameResult") ?? 1;
#else
        return 1;
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _fairyInstance?.Call("destroy");
        _fairyInstance?.Dispose();
#endif
    }

    // 콜백 프록시
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