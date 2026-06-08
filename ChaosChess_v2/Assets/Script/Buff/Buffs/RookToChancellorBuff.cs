using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/RookToChancellor")]
public class RookToChancellorBuff : BuffSO
{
    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        if (BoardManager.Instance == null || magnitude <= 0) return;

        PieceColor targetColor = side == BuffSide.Buff ? PieceColor.White : PieceColor.Black;
        List<Rook> rooks = BoardManager.Instance.GetPiece<Rook>(targetColor);
        if (rooks == null || rooks.Count == 0) return;

        int convertCount = Mathf.Min(magnitude, rooks.Count);
        for (int i = 0; i < convertCount; i++)
        {
            int randomIndex = Random.Range(0, rooks.Count);
            Rook target = rooks[randomIndex];
            rooks.RemoveAt(randomIndex);

            BoardManager.Instance.ChangePiece(target.Pos, targetColor, 'y');
        }
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
        // 기물 변환은 비가역 효과로 유지
    }
}
