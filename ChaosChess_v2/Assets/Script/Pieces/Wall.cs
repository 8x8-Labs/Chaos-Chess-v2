using UnityEngine;

public class Wall : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "A";
        else
            return "a";
    }
}
