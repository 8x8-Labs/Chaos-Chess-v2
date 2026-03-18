using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    [SerializeField] private List<Vector2Int> AttackOffsets;
    [SerializeField] private List<Vector2Int> EnPassantCheckOffsets;
    [SerializeField] private Vector2Int FirstMoveOffset;

    private int StartPosY;

    public override void Init(Vector3Int pos, PieceColor color)
    {
        Color = color;
        Pos = pos;

        if (Color == PieceColor.White)
        {
            StartPosY = 1;

            spriteRenderer.sprite = WhitePiece;
        }
        else
        {
            StartPosY = 6;

            spriteRenderer.sprite = BlackPiece;

            FirstMoveOffset *= -1;
            for (int i = 0; i < AttackOffsets.Count; i++)
            {
                AttackOffsets[i] *= -1;
            }

            for (int i = 0; i < CanMoveOffsets.Count; i++)
            {
                CanMoveOffsets[i] *= -1;
            }
        }
    }

    public override void UpdateCanMovePos(BoardManager board)
    {
        CanMovePos = new List<Vector3Int>();
        Vector3Int target;
        foreach (Vector2Int CanMoveOffset in CanMoveOffsets)
        {
            target = new Vector3Int(Pos.x, Pos.y + CanMoveOffset.y, 0);
            if (board.IsEmpty(target))
            {
                CanMovePos.Add(target);
            }
        }
        Debug.Log(Pos.y + " " + StartPosY);
        if (Pos.y == StartPosY)
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

    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "P";
        else
            return "p";
    }

}