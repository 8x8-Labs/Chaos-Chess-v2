using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 돌격 - 전역 (희귀)
/// 모든 폰이 앞으로 한 칸 전진합니다.
/// </summary>
public class ChargeCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        BoardManager bm = BoardManager.Instance;

        PieceColor myColor = args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor();
        int advanceDir = GetAdvanceDirection(myColor);

        List<Piece> myPawns = bm.GetAllPieces()
            .FindAll(p => p.Color == myColor && p.Type == PieceType.Pawn);

        myPawns.Sort((a, b) => advanceDir > 0
            ? b.Pos.y.CompareTo(a.Pos.y)
            : a.Pos.y.CompareTo(b.Pos.y));

        int movedCount = 0;
        foreach (Piece pawn in myPawns)
        {
            if (pawn == null)
                continue;

            Vector3Int target = new Vector3Int(pawn.Pos.x, pawn.Pos.y + advanceDir, 0);

            if (!bm.IsInside(target)) continue;
            if (!bm.IsEmpty(target)) continue;

            char promotion = IsPromotionRow(myColor, target.y) ? 'q' : '\0';
            bm.ForceTeleport(pawn, target, promotion);
            movedCount++;
        }

        Debug.Log($"[Charge] caster={myColor}, direction={advanceDir}, movedPawns={movedCount}.");
    }

    private static int GetAdvanceDirection(PieceColor color)
    {
        return color == PieceColor.White ? 1 : -1;
    }

    private static bool IsPromotionRow(PieceColor color, int y)
    {
        return y == (color == PieceColor.White ? 7 : 0);
    }
}
