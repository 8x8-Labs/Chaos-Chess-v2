using UnityEngine;

public class GamaManager : MonoBehaviour
{
    private PieceColor turn;

    private BoardManager boardManager;
    private BoardUI boardUI;

    private Piece selectedPiece;


    void Start()
    {
        turn = PieceColor.White;

        boardManager = GetComponent<BoardManager>();
        boardUI = GetComponent<BoardUI>();
    }

    public void SelectGrid(Vector3Int pos)
    {
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

    private void MoveSelected(Vector3Int target, BoardManager boardManager)
    {
        if (selectedPiece == null)
            return;

        boardManager.MovePiece(selectedPiece, target);
        boardManager.UpdatePiecesCanMovePos();

        NextTurn();
    }

    private void NextTurn()
    {
        if (turn == PieceColor.White)
            turn = PieceColor.Black;
        else
            turn = PieceColor.White;
    }
}
