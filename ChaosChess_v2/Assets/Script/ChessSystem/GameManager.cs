using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private int curTurn = 1;
    public bool IsPlayerTurn => (curTurn % 2 == 0);

    public bool IsGameInput = true;

    public char NowTurn
    {
        get
        {
            if (curTurn % 2 == 0)
                return 'w';
            else
                return 'b';
        }
    }
    public PieceColor turnColor
    {
        get
        {
            if (curTurn % 2 == 0)
                return PieceColor.White;
            else
                return PieceColor.Black;
        }
    }

    private UIManager uiManager;
    private BoardManager boardManager;
    private BoardUI boardUI;

    private Piece selectedPiece;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        FairyStockfishBridge.Instance.InitEngine("chess");

        curTurn = 1;

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
        if (!IsPlayerTurn) return;
        if (!boardManager.IsInside(pos)) return;

        Piece piece = boardManager.GetPiece(pos);

        if (piece != null && piece.Color == turnColor)
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
        IsGameInput = false;

        uiManager.Show((type) =>
        {
            boardManager.CreatePromotionPiece(pos, pawn.Color, type);

            IsGameInput = true;

            NextTurn();

            RequestAIMove();
        });
    }

    private void SelectPiece(Piece piece)
    {
        selectedPiece = piece;
    }

    public void NextTurn()
    {
        curTurn += 1;
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
            if (!IsGameInput)
                return;

            NextTurn();

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
            }
        );
    }
    private void EvaluateGameState(string[] moves)
    {
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
