using UnityEngine;

public class ChessGameManager : MonoBehaviour
{
    private const string START_FEN =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private string _currentFen = START_FEN;
    private string _moveHistory = "";
    public bool isPlayerTurn = true;

    void Start()
    {
        NewGame();
    }

    public void NewGame(string variant = "chess")
    {
        _currentFen = START_FEN;
        _moveHistory = "";
        isPlayerTurn = true;
        FairyStockfishBridge.Instance.InitEngine(variant);
        FairyStockfishBridge.Instance.SetPosition(_currentFen);
        Debug.Log("[Game] 새 게임 시작");
    }

    public void PlayerMove(string uciMove)
    {
        if (!isPlayerTurn) return;

        string[] legalMoves = FairyStockfishBridge.Instance.GetLegalMoves();
        bool isLegal = System.Array.IndexOf(legalMoves, uciMove) >= 0;

        if (!isLegal)
        {
            Debug.LogWarning("[Game] 불법적인 수: " + uciMove);
            return;
        }

        ApplyMove(uciMove);
        isPlayerTurn = false;

        int result = FairyStockfishBridge.Instance.GetGameResult();
        if (result != 1)
        {
            OnGameEnd(result);
            return;
        }

        // AI 수 요청
        RequestAIMove();
    }

    public void RequestAIMove()
    {
        Debug.Log("[Game] AI 생각 중...");
        FairyStockfishBridge.Instance.GetBestMoveAsync(
            depth: 12,
            moveTimeMs: 2000,
            callback: (move) =>
            {
                Debug.Log("[Game] AI 수: " + move);
                ApplyMove(move);
                isPlayerTurn = true;

                // UI 업데이트
                ChessTestUI ui = FindFirstObjectByType<ChessTestUI>();
                if (ui != null)
                {
                    ui.OnAIMoveCompleted(move);  // ← AI 수 UI에 표시
                }

                int result = FairyStockfishBridge.Instance.GetGameResult();
                if (result != 1) OnGameEnd(result);
            }
        );
    }

    private void ApplyMove(string uciMove)
    {
        if (_moveHistory.Length > 0) _moveHistory += " ";
        _moveHistory += uciMove;
        FairyStockfishBridge.Instance.SetPosition(_currentFen, _moveHistory);
        Debug.Log("[Game] 무브 적용: " + uciMove);
    }

    private void OnGameEnd(int result)
    {
        string msg = result == -1 ? "체크메이트!" : "스테일메이트!";
        Debug.Log("[Game] 게임 종료: " + msg);
    }
}