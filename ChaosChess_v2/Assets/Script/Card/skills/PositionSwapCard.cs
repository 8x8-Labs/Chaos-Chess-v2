using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 입장 반전 - 전역
/// 자신의 기물 전체와 상대 기물 전체를 교환하며 기물에 적용된 카드 효과가 전부 사라집니다.
/// 기물 수가 다를 경우 적은 쪽의 수만큼 교환됩니다.
/// </summary>
public class PositionSwapCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        BoardManager bm = BoardManager.Instance;

        PieceColor myColor = GameManager.Instance.PlayerColor;
        PieceColor oppColor = GameManager.Instance.EnemyColor;

        List<Piece> myPieces = bm.GetAllPieces().FindAll(p => p.Color == myColor);
        List<Piece> oppPieces = bm.GetAllPieces().FindAll(p => p.Color == oppColor);

        int swapCount = Mathf.Min(myPieces.Count, oppPieces.Count);

        // 교환 전 위치 저장
        List<Vector3Int> myPositions = myPieces.ConvertAll(p => p.Pos);
        List<Vector3Int> oppPositions = oppPieces.ConvertAll(p => p.Pos);

        // 두 그룹을 하나의 배치 작업으로 처리 (Phase 1 clear → Phase 2 place)
        List<Piece> batch = new List<Piece>(swapCount * 2);
        List<Vector3Int> newPositions = new List<Vector3Int>(swapCount * 2);

        for (int i = 0; i < swapCount; i++)
        {
            batch.Add(myPieces[i]);
            newPositions.Add(oppPositions[i]);

            batch.Add(oppPieces[i]);
            newPositions.Add(myPositions[i]);
        }

        bm.BatchReassign(batch, newPositions);

        // TODO: 기물에 적용된 카드 효과 제거 (효과 시스템 구현 후 처리)
    }
}
