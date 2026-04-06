using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 돌려돌려돌림판 - 전역 (전설)
/// 상대 기물(킹, 퀸 제외)의 위치를 전부 섞습니다.
/// </summary>
public class ShuffleBoardCard : CardData, ICard
{
    [ContextMenu("Execute")]
    public void ex()
    {
        Execute();
    }
    public void Execute(CardEffectArgs args = null)
    {
        BoardManager bm = BoardManager.Instance;

        PieceColor myColor = GameManager.Instance.PlayerColor;
        PieceColor oppColor = GameManager.Instance.EnemyColor;

        // 킹, 퀸을 제외한 상대 기물
        List<Piece> targets = bm.GetAllPieces()
            .FindAll(p => p.Color == oppColor
                       && p.Type != PieceType.King
                       && p.Type != PieceType.Queen);

        if (targets.Count < 2) return;

        // 현재 위치 수집
        List<Vector3Int> positions = targets.ConvertAll(p => p.Pos);

        // Fisher-Yates 셔플
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector3Int tmp = positions[i];
            positions[i] = positions[j];
            positions[j] = tmp;
        }

        bm.BatchReassign(targets, positions);
    }
}
