using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/RandomMinorToWall")]
public class RandomMinorToWallBuff : BuffSO
{
    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        if (BoardManager.Instance == null || magnitude <= 0) return;

        // Buff: 상대(Black) 마이너 기물 약화, Debuff: 아군(White) 마이너 기물 약화
        PieceColor targetColor = side == BuffSide.Buff ? PieceColor.Black : PieceColor.White;
        List<Piece> minors = GetMinorPieces(targetColor);
        if (minors.Count == 0) return;

        int convertCount = Mathf.Min(magnitude, minors.Count);
        for (int i = 0; i < convertCount; i++)
        {
            int randomIndex = Random.Range(0, minors.Count);
            Piece target = minors[randomIndex];
            minors.RemoveAt(randomIndex);

            BoardManager.Instance.ChangePiece(target.Pos, targetColor, 'a');
        }
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
        // 벽 변환은 비가역 효과로 유지
    }

    private static List<Piece> GetMinorPieces(PieceColor color)
    {
        List<Piece> result = new List<Piece>();
        List<Knight> knights = BoardManager.Instance.GetPiece<Knight>(color);
        List<Bishop> bishops = BoardManager.Instance.GetPiece<Bishop>(color);

        if (knights != null)
        {
            foreach (Knight knight in knights)
            {
                if (knight != null) result.Add(knight);
            }
        }

        if (bishops != null)
        {
            foreach (Bishop bishop in bishops)
            {
                if (bishop != null) result.Add(bishop);
            }
        }

        return result;
    }
}
