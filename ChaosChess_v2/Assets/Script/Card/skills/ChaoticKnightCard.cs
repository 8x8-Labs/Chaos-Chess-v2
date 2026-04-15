using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 혼란의 나이트 - 기물 전용 (희귀)
/// 나이트 기물이 5x5 위치 중 가능한 칸으로 이동할 수 있습니다.
/// </summary>
public class ChaoticKnightCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        List<Piece> pieces = args.Targets;
        // TODO: 선택된 나이트에게 이번 턴 5x5 범위 내 이동 가능한 칸으로 이동 허용 처리

        foreach (Piece knight in pieces)
        {
            List<Vector3Int> movableCells = GetMovableCellsIn5x5(knight);

            if (movableCells.Count == 0) continue;

            Vector3Int destination = movableCells[Random.Range(0, movableCells.Count)];
            Debug.Log(destination.ToString());
            BoardManager.Instance.ForceTeleport(knight, destination);
        }
    }

    private List<Vector3Int> GetMovableCellsIn5x5(Piece knight)
    {
        List<Vector3Int> result = new List<Vector3Int>();
        Vector3Int origin = knight.Pos;

        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                if (x == 0 && y == 0) continue;

                Vector3Int candidate = new Vector3Int(origin.x + x, origin.y + y, origin.z);

                // 보드 범위 체크 및 이동 가능 여부 확인
                if (BoardManager.Instance.IsInside(candidate) && !IsOccupied(candidate))
                {
                    result.Add(candidate);
                }
            }
        }

        return result;
    }

    private bool IsOccupied(Vector3Int candidate)
    {
        Piece p = BoardManager.Instance.GetPiece(candidate);
        return p != null;
    }
}
