using UnityEngine;
using UnityEngine.UI;

public class EngineTestUI : MonoBehaviour
{
    [Header("Bridge 연결")]
    [SerializeField] private FairyStockfishBridge engine;

    [Header("UI 연결")]
    [SerializeField] private InputField fenInput;      // FEN 입력창
    [SerializeField] private Text resultText;    // 결과 출력
    [SerializeField] private Button requestButton; // 요청 버튼

    // 테스트용 기본 FEN (체스 초기 포지션)
    private const string DEFAULT_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    void Start()
    {
        engine.OnBestMove += HandleBestMove;
        engine.OnInfo += HandleInfo;
        engine.OnReady += HandleReady;

        requestButton.onClick.AddListener(OnRequestClicked);

        // 기본값 채우기
        if (fenInput != null)
            fenInput.text = DEFAULT_FEN;

        resultText.text = "엔진 초기화 중...";
    }

    private void HandleReady()
    {
        resultText.text = "✅ 엔진 준비 완료\nFEN을 입력하고 버튼을 눌러주세요.";
        requestButton.interactable = true;
    }

    private void OnRequestClicked()
    {
        string fen = fenInput != null && fenInput.text.Trim() != ""
            ? fenInput.text.Trim()
            : DEFAULT_FEN;

        resultText.text = $"🔍 분석 중...\nFEN: {fen}";
        requestButton.interactable = false;

        engine.RequestBestMoveByTime(fen, msec: 1500);
    }

    private void HandleBestMove(string move)
    {
        resultText.text = $"✅ 최선수: {move}\n\nFEN: {fenInput?.text}";
        requestButton.interactable = true;

        Debug.Log($"[Test] bestmove = {move}");
    }

    private void HandleInfo(string info)
    {
        // depth, score, pv 파싱해서 표시
        if (info.Contains("depth") && info.Contains("score"))
        {
            string depth = ExtractValue(info, "depth");
            string score = ExtractValue(info, "score cp");
            string pv = ExtractValue(info, "pv");

            if (depth != null)
                Debug.Log($"[Info] depth={depth} score={score} pv={pv}");
        }
    }

    private string ExtractValue(string info, string key)
    {
        int idx = info.IndexOf(key);
        if (idx < 0) return null;
        string[] parts = info.Substring(idx + key.Length).Trim().Split(' ');
        return parts.Length > 0 ? parts[0] : null;
    }
}