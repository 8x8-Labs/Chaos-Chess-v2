using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/RandomPawnToWall")]
public class RandomPawnToWallBuff : BuffSO
{
    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        if (BoardManager.Instance == null) return;

        // Buff: 상대(Black) 폰 약화, Debuff: 아군(White) 폰 약화
        PieceColor targetColor = side == BuffSide.Buff ? PieceColor.Black : PieceColor.White;
        List<Pawn> pawns = BoardManager.Instance.GetPiece<Pawn>(targetColor);
        if (pawns == null || pawns.Count == 0) return;

        int count = Mathf.Min(magnitude, pawns.Count);
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, pawns.Count);
            Piece target = pawns[randomIndex];
            pawns.RemoveAt(randomIndex);

            BoardManager.Instance.ChangePiece(target.Pos, targetColor, 'a');
        }
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
        // 벽 변환은 비가역 효과로 유지
    }
}
