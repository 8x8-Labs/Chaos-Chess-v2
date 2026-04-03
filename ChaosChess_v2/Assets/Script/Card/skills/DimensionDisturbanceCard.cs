using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차원 교란 - 기물 전용
/// 상대 기물 2개를 비어있는 랜덤한 칸으로 이동시킨다.
/// </summary>
public class DimensionDisturbanceCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void Execute(CardEffectArgs args = null)
    {
        List<Piece> targets = args.Targets;

        foreach (Piece target in targets)
        {
            List<Vector3Int> emptyCells = GetMovableCells();
            if (emptyCells.Count == 0) continue;

            Vector3Int destination = emptyCells[Random.Range(0, emptyCells.Count)];
            BoardManager.Instance.ForceTeleport(target, destination);
        }
    }

    private List<Vector3Int> GetMovableCells()
    {
        List<Vector3Int> result = new List<Vector3Int>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Vector3Int candidate = new Vector3Int(x, y, 0);
                if (BoardManager.Instance.IsEmpty(candidate))
                    result.Add(candidate);
            }
        }

        return result;
    }

    [ContextMenu("Execute")]
    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }
}
