using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/RandomPieceRemoval")]
public class RandomPieceRemovalBuff : BuffSO
{
    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        if (BoardManager.Instance == null || magnitude <= 0) return;

        PieceColor targetColor = side == BuffSide.Buff ? PieceColor.Black : PieceColor.White;
        List<Piece> candidates = GetRemovablePieces(targetColor);
        if (candidates.Count == 0) return;

        int removeCount = Mathf.Min(magnitude, candidates.Count);
        for (int i = 0; i < removeCount; i++)
        {
            int randomIndex = Random.Range(0, candidates.Count);
            Piece target = candidates[randomIndex];
            candidates.RemoveAt(randomIndex);

            BoardManager.Instance.DestroyPiece(target, i == removeCount - 1);
        }
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
        // 제거 효과는 비가역으로 유지
    }

    private static List<Piece> GetRemovablePieces(PieceColor color)
    {
        return BoardManager.Instance.GetAllPieces().FindAll(piece =>
            piece != null &&
            piece.Color == color &&
            piece.Type != PieceType.King &&
            piece.Type != PieceType.Queen &&
            piece.Type != PieceType.Wall);
    }
}
