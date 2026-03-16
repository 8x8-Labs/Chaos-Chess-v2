using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    public override List<Vector3Int> GetLegalMoves(BoardManager board)
    {
        List<Vector3Int> moves = new List<Vector3Int>();

        int dir = Color == PieceColor.White ? 1 : -1;

        Vector3Int forward = BoardPos + new Vector3Int(0, dir);
        
        if (board.IsEmpty(forward))
            moves.Add(forward);

        return moves;
    }
}