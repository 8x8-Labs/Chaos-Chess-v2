using System.Collections.Generic;
using UnityEngine;

/// <summary>적용 시 플레이어의 랜덤한 폰 하나를 무작위 기물로 승급시키는 버프</summary>
[CreateAssetMenu(menuName = "Buff/RandomPawnPromotionBuff")]
public class RandomPawnPromotionBuff : BuffSO
{
    private static readonly PieceType[] PromotionTargets =
    {
        PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight
    };

    public override void OnApply(Player player)
    {

        List<Pawn> pawns = BoardManager.Instance.GetPiece<Pawn>(PieceColor.White);
        if (pawns == null || pawns.Count == 0) return;

        Piece target = pawns[Random.Range(0, pawns.Count)];
        PieceType promoteTo = PromotionTargets[Random.Range(0, PromotionTargets.Length)];

        char promoteType = ' ';
        switch (promoteTo)
        {
            case PieceType.Queen:
                promoteType = 'q';
                break;
            case PieceType.Rook:
                promoteType = 'r';
                break;
            case PieceType.Bishop:
                promoteType = 'b';
                break;
            case PieceType.Knight:
                promoteType = 'n';
                break;
        }

        BoardManager.Instance.ChangePiece(target.Pos, PieceColor.White, promoteType);
    }

    public override void OnRemove(Player player) { }
}
