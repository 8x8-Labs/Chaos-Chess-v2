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

        PieceColor myColor = GameManager.Instance.PlayerColor;
        int advanceDir = 1;
        int promotionRow = 7;

        List<Piece> myPawns = bm.GetAllPieces()
            .FindAll(p => p.Color == myColor && p.Type == PieceType.Pawn);

        foreach (Piece pawn in myPawns)
        {
            Vector3Int target = new Vector3Int(pawn.Pos.x, pawn.Pos.y + advanceDir, 0);

            if (!bm.IsInside(target)) continue;
            if (!bm.IsEmpty(target)) continue;

            bm.ForceTeleport(pawn, target);

            // 프로모션 행 도달 시 승격 이벤트 발생
            if (target.y == promotionRow)
                bm.OnPromotionRequired?.Invoke(pawn, target);
        }
    }
}
