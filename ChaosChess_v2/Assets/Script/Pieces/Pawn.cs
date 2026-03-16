using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    [SerializeField] private List<Vector2Int> AttackOffsets;
    [SerializeField] private Vector2Int FirstMoveOffset;

    public override void UpdateCanMovePos(BoardManager board)
    {
        CanMovePos = new List<Vector3Int>();

        Vector3Int target = new Vector3Int(Pos.x, Pos.y + 1, 0);
        if (board.IsEmpty(target))
            CanMovePos.Add(target);

        if (Pos.y == 1)
        {
            target = new Vector3Int(Pos.x + FirstMoveOffset.x, Pos.y + FirstMoveOffset.y, 0);
            if (board.IsEmpty(target))
                CanMovePos.Add(target);
        }

        foreach (Vector2Int AttackOffset in AttackOffsets)
        {
            target = new Vector3Int(Pos.x + AttackOffset.x, Pos.y + AttackOffset.y, 0);
            if (board.IsInside(target) && !board.IsEmpty(target))
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