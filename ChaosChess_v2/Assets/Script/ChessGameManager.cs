using System.Collections.Generic;
using UnityEngine;


public class ChessGameManager : MonoBehaviour
{
    [SerializeField] private FairyStockfishBridge engine;


    private List<string> _moveHistory = new List<string>();
    private bool _isWhiteTurn = true;


    void Start()
    {
        engine.OnReady += HandleEngineReady;
        engine.OnBestMove += HandleBestMove;
        engine.OnInfo += HandleInfo;
        engine.OnError += HandleError;
    }


    private void HandleEngineReady()
    {
        Debug.Log("[Chess] 엔진 준비 완료");
        // 커스텀 변형 설정
        engine.SetVariant("archchess");
        // 탐색 스레드 수 설정
        engine.SetOption("Threads", "2");
        // 해시 테이블 크기 (MB)
        engine.SetOption("Hash", "64");
    }


    // 플레이어가 수를 뒀을 때 호출
    public void OnPlayerMove(string uciMove)
    {
        _moveHistory.Add(uciMove);
        ApplyMoveToBoard(uciMove);
        _isWhiteTurn = !_isWhiteTurn;


        // AI 차례에 수 요청
        engine.RequestBestMoveFromMoves(
            _moveHistory.ToArray(),
            msec: 1500
        );
    }


    private void HandleBestMove(string move)
    {
        if (move == "(none)") { Debug.Log("게임 종료"); return; }


        _moveHistory.Add(move);
        ApplyMoveToBoard(move);
        _isWhiteTurn = !_isWhiteTurn;
        Debug.Log($"[AI] 최선수: {move}");
    }


    private void ApplyMoveToBoard(string uciMove)
    {
        // 드롭 수: "P@f7" → 기물타입@칸
        if (uciMove.Contains("@"))
        {
            char pieceChar = uciMove[0];
            string square = uciMove.Substring(2, 2);
            DropPiece(pieceChar, square);
            return;
        }


        // 일반 수: "e2e4" 또는 프로모션 "a7a8q"
        string from = uciMove.Substring(0, 2);
        string to = uciMove.Substring(2, 2);
        char? promo = uciMove.Length > 4 ? uciMove[4] : (char?)null;
        MovePiece(from, to, promo);
    }


    private void MovePiece(string from, string to, char? promotion)
    {
        // TODO: 보드 UI 업데이트
        Debug.Log($"이동: {from} → {to}" + (promotion.HasValue ? $" 프로모션: {promotion}" : ""));
    }


    private void DropPiece(char piece, string square)
    {
        // TODO: 기물 드롭 UI 처리
        Debug.Log($"드롭: {piece} → {square}");
    }


    private void HandleInfo(string info)
    {
        // info depth 15 score cp 42 pv e2e4 e7e5 ...
        // 분석 정보 파싱 (UI에 표시할 경우)
    }


    private void HandleError(string error)
    {
        Debug.LogError($"[Fairy] 오류: {error}");
    }
}
