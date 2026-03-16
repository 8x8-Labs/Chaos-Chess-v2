using UnityEngine;

public class GamaManager : MonoBehaviour
{
    private PieceColor turn = PieceColor.White;

    private Piece selectedPiece;

    public void SelectPiece(Piece piece)
    {
        if (piece.Color != turn)
            return;

        selectedPiece = piece;
    }

    public void MoveSelected(Vector3Int target, BoardManager boardManager)
    {
        if (selectedPiece == null)
            return;

        boardManager.MovePiece(selectedPiece, target);

        EndTurn();
    }

    void EndTurn()
    {
        if (turn == PieceColor.White)
            turn = PieceColor.Black;
        else
            turn = PieceColor.White;
    }
}
