using System.Collections.Generic;
using UnityEngine;

public class Knight : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "N";
        else
            return "n";
    }
}
