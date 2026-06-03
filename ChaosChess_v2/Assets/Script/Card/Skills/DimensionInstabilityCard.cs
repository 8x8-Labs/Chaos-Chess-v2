using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차원 불안 - 기물 전용 (레어)
/// 나이트에 적용 가능, 이동 시 가상의 나이트가 이동합니다.
/// 나이트가 잡힐 시 가상의 나이트의 위치로 이동합니다.
/// </summary>
public class DimensionInstabilityCard : CardData, IPieceCard
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
        Piece piece = args.Targets[0];
        var effector = CreatePieceEffector<DimensionInstabilityEffector>(piece);
        effector.Apply();
    }
}

public class DimensionInstabilityEffector : PieceEffector
{
    private Vector3Int ghostPos;
    private bool hasGhost;

    protected override void OnApply()
    {
        hasGhost = false;
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        hasGhost = TryMoveGhost(target.PrevPos, dest);
    }

    public override void OnPieceCaptured()
    {
        if (hasGhost && BoardManager.Instance.IsInside(ghostPos) && BoardManager.Instance.IsEmpty(ghostPos))
        {
            BoardManager.Instance.ChangePiece(ghostPos, target.Color, 'n');
        }

        Revert();
    }

    protected override void OnRevert()
    {
        Destroy(this);
    }

    private bool TryMoveGhost(Vector3Int origin, Vector3Int realDestination)
    {
        List<Vector3Int> cells = GetMovableCells(origin);
        cells.Remove(realDestination);
        if (cells.Count == 0)
        {
            ghostPos = realDestination;
            return true;
        }

        ghostPos = cells[Random.Range(0, cells.Count)];
        return true;
    }

    private List<Vector3Int> GetMovableCells(Vector3Int origin)
    {
        List<Vector3Int> result = new List<Vector3Int>();
        int[] dx = { -2, -2, -1, 1, 2, 2, 1, -1 };
        int[] dy = { -1, 1, 2, 2, 1, -1, -2, -2 };
        for (int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];

            Vector3Int candidate = new Vector3Int(origin.x + x, origin.y + y, origin.z);

            if (!BoardManager.Instance.IsInside(candidate)) continue;

            Piece piece = BoardManager.Instance.GetPiece(candidate);
            if (piece == null || piece.Color != target.Color)
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}
