using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "P";
        else
            return "p";
    }
}