using System.Collections.Generic;
using UnityEngine;

public class Knight : Piece
{
    public override void UpdateCanMovePos(BoardManager board)
    {
        CanMovePos = new List<Vector3Int>();

        foreach (Vector2Int CanMoveOffset in CanMoveOffsets)
        {
            Vector3Int target = new Vector3Int(Pos.x + CanMoveOffset.x, Pos.y + CanMoveOffset.y, 0);
            if (board.IsInside(target))
            {
                if (board.IsEmpty(target))
                {
                    CanMovePos.Add(target);
                }
                else
                {
                    Piece piece = board.GetPiece(target);
                    if (piece.Color != Color)
                    {
                        CanMovePos.Add(target);
                    }
                }
            }
        }
    }
}
