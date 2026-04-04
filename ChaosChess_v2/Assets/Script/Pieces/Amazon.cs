using UnityEngine;

public class Amazon : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "S";
        else
            return "s";
    }
}
