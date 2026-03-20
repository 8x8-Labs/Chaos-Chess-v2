using UnityEngine;

public class GamaManager : MonoBehaviour
{
    public bool isPlayerTurn = true; // 추가
    private PieceColor turn;

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

    private BoardManager boardManager;
    private BoardUI boardUI;

    private Piece selectedPiece;


    void Start()
    {
        turn = PieceColor.White;

        boardManager = GetComponent<BoardManager>();
        boardUI = GetComponent<BoardUI>();

        FairyStockfishBridge.Instance.InitEngine("chess");
    }

    public void SelectGrid(Vector3Int pos)
    {
        if (!isPlayerTurn) return;

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

    private void SelectPiece(Piece piece)
    {
        selectedPiece = piece;
    }

    public void NextTurn()
    {
        boardManager.UpdatePiecesCanMovePos();

        if (turn == PieceColor.White)
            turn = PieceColor.Black;
        else
            turn = PieceColor.White;

        boardManager.UpdateFEN(); // 디버깅
        Debug.Log(boardManager.GetFEN());
    }

    // MoveSelected 안에서 플레이어 수 적용 후:
    private void MoveSelected(Vector3Int target, BoardManager boardManager)
    {
        if (selectedPiece == null) return;

        if (boardManager.MovePiece(selectedPiece, target))
        {
            selectedPiece = null;
            NextTurn();

            // FEN을 BoardManager에서 뽑아서 엔진에 전달
            Debug.Log(boardManager);
            string fen = boardManager.GetFEN();
            FairyStockfishBridge.Instance.SetPosition(fen);

            // AI에게 수 요청
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
                ApplyUCIMove(uciMove);
                isPlayerTurn = true;
            }
        );
    }

    private void ApplyUCIMove(string uciMove)
    {
        // "e2e4" → from(4,1), to(4,3) 변환
        Vector3Int from = UCIToGrid(uciMove.Substring(0, 2));
        Vector3Int to = UCIToGrid(uciMove.Substring(2, 2));

        Piece piece = boardManager.GetPiece(from);
        if (piece != null)
            boardManager.MovePiece(piece, to);

        NextTurn();
    }

    private Vector3Int UCIToGrid(string sq)
    {
        int x = sq[0] - 'a'; // 'a'~'h' → 0~7
        int y = sq[1] - '1'; // '1'~'8' → 0~7
        return new Vector3Int(x, y, 0);
    }

}
