using System.Collections.Generic;
using UnityEngine;

/// <summary>적용 시 플레이어의 랜덤한 폰 하나를 무작위 기물로 승급시키는 버프</summary>
public class RandomPawnPromotionBuff : IPlayerBuff
{
    private static readonly PieceType[] PromotionTargets =
    {
        PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight
    };

    public void OnApply(Player player)
    {
        
        List<Pawn> pawns = BoardManager.Instance.GetPiece<Pawn>(PieceColor.White);
        if (pawns == null || pawns.Count == 0) return;

        Piece target = pawns[Random.Range(0, pawns.Count)];
        PieceType promoteTo = PromotionTargets[Random.Range(0, PromotionTargets.Length)];
        // 기물 승격시키기
        // BoardManager.Instance.ChangePiece(target.Pos, PieceColor.White, )
    }

    public void OnRemove(Player player) { }
}
