using UnityEngine;

public class GameManager : MonoBehaviour
{
    private bool isPlayerTurn = true;
    public bool IsPlayerTurn => isPlayerTurn;
    private PieceColor turn;

    private bool isWaitingPromotion = false;
    public bool IsWaitingPromotion
    {
        get { return isWaitingPromotion; }
        set { isWaitingPromotion = value; }
    }

    public char NowTurn
    {
        get
        {
            if (turn == PieceColor.White)
                return 'w';
            else
                return 'b';
        }
    }

    private UIManager uiManager;
    private BoardManager boardManager;
    private BoardUI boardUI;

    private Piece selectedPiece;


    void Start()
    {
        FairyStockfishBridge.Instance.InitEngine("chess");

        turn = PieceColor.White;

        boardManager = GetComponent<BoardManager>();
        boardUI = GetComponent<BoardUI>();
        uiManager = FindFirstObjectByType<UIManager>();

        boardManager.OnPromotionRequired += HandlePromotion;

        boardManager.LoadFEN();

        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        EvaluateGameState(moves);
        boardManager.UpdatePiecesCanMovePos(moves);
    }

    public void SelectGrid(Vector3Int pos)
    {
        if (!isPlayerTurn) return;
        if (!boardManager.IsInside(pos)) return;

        Piece piece = boardManager.GetPiece(pos);

        if (piece != null && piece.Color == turn)
        {
            SelectPiece(piece);
            boardUI.DrawSelectTile(pos);
        }
        else
        {
            MoveSelected(pos, boardManager);
            boardUI.DeleteSelectTile();
        }
    }

    private void HandlePromotion(Piece pawn, Vector3Int pos)
    {
        isWaitingPromotion = true;

        uiManager.Show((type) =>
        {
            boardManager.CreatePromotionPiece(pos, pawn.Color, type);

            isWaitingPromotion = false;

            NextTurn();

            isPlayerTurn = false;
            RequestAIMove();
        });
    }

    private void SelectPiece(Piece piece)
    {
        selectedPiece = piece;
    }

    public void NextTurn()
    {
        if (turn == PieceColor.White)
        {
            turn = PieceColor.Black;
        }
        else
        {
            turn = PieceColor.White;
        }
        boardManager.UpdateFEN(); // 디버깅
        string fen = boardManager.GetFEN();
        FairyStockfishBridge.Instance.SetPosition(fen);

        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        EvaluateGameState(moves);
        boardManager.UpdatePiecesCanMovePos(moves);
    }

    // MoveSelected 안에서 플레이어 수 적용 후:
    private void MoveSelected(Vector3Int target, BoardManager boardManager)
    {
        if (selectedPiece == null) return;

        if (boardManager.MovePiece(selectedPiece, target))
        {
            selectedPiece = null;

            // 프로모션이면 여기서 멈춤
            if (isWaitingPromotion)
                return;

            NextTurn();

            isPlayerTurn = false;
            RequestAIMove();
        }
    }

    private void RequestAIMove()
    {
        FairyStockfishBridge.Instance.GetBestMoveAsync(
            depth: 12,
            moveTimeMs: 2000,
            callback: (uciMove) =>
            {
                // UCI 수 (예: "e2e4") → Vector3Int 변환 후 BoardManager에 적용
                boardManager.ApplyUCIMove(uciMove);
                isPlayerTurn = true;
            }
        );
    }
    private void EvaluateGameState(string[] moves)
    {
        Debug.Log(boardManager.GetHalfmoveClock());
        if (boardManager.GetHalfmoveClock() >= 150)
        {
            OnDraw();
        }
        bool isCheck = FairyStockfishBridge.Instance.IsInCheck();


        if (moves.Length == 0)
        {
            if (isCheck)
                OnCheckmate();
            else
                OnDraw();
        }
        else if (isCheck)
        {
            OnCheck();
        }
    }
    private void OnCheck()
    {
        Debug.Log("체크");
    }

    private void OnCheckmate()
    {
        if (NowTurn == 'w')
        {
            Debug.Log("체크메이트");
            Debug.Log("흑 승");
        }
        else
        {
            Debug.Log("체크메이트");
            Debug.Log("백 승");
        }
        ExitGame();
    }

    private void OnDraw()
    {
        Debug.Log("무승부");
        ExitGame();
    }

    // (임시) 게임 종료 메서드
    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
