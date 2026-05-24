using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/RandomPawnPromotion")]
public class RandomPawnPromotionBuff : BuffSO
{
    private static readonly PieceType[] PromotionTargets =
    {
        PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight
    };

    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        if (BoardManager.Instance == null) return;

        PieceColor color = side == BuffSide.Buff ? PieceColor.White : PieceColor.Black;
        List<Pawn> pawns = BoardManager.Instance.GetPiece<Pawn>(color);
        if (pawns == null || pawns.Count == 0) return;

        for (int i = 0; i < magnitude; i++)
        {
            Piece target = pawns[Random.Range(0, pawns.Count)];
            PieceType promoteTo = PromotionTargets[Random.Range(0, PromotionTargets.Length)];
            BoardManager.Instance.ChangePiece(target.Pos, color, ToPromotionChar(promoteTo));
        }
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
    }

    private static char ToPromotionChar(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.Queen: return 'q';
            case PieceType.Rook: return 'r';
            case PieceType.Bishop: return 'b';
            case PieceType.Knight: return 'n';
            default: return 'q';
        }
    }
}
