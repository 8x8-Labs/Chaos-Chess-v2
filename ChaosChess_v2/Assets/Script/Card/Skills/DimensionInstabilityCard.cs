using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차원 불안 - 기물 전용 (레어)
/// 나이트에 적용 가능, 이동 시 가상의 나이트가 랜덤한 위치로 이동하며 
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
    List<Vector3Int> cells;
    bool flag = false;
    protected override void OnApply()
    {
        cells = GetMovableCells(target);
    }
    public override void OnPieceMove(Vector3Int dest)
    {
        Debug.Log("move");
        flag = true;
        if (cells.Contains(dest))
        {
            cells.Remove(dest);
        }
    }
    public override void OnPieceCaptured()
    {
        Debug.Log(cells.Count);
        if (cells.Count == 0)
            return;
        if (flag)
        {
            Vector3Int pos = cells[Random.Range(0, cells.Count)];
            BoardManager.Instance.ChangePiece(pos, target.Color, 'n');
        }
    }
    protected override void OnRevert()
    {
        Debug.Log("revert");
        Destroy(this);
    }
    private List<Vector3Int> GetMovableCells(Piece knight)
    {
        List<Vector3Int> result = new List<Vector3Int>();
        Vector3Int origin = knight.Pos;
        int[] dx = { -2, -2, -1, 1, 2, 2, 1, -1 };
        int[] dy = { -1, 1, 2, 2, 1, -1, -2, -2 };
        for (int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];

            Vector3Int candidate = new Vector3Int(origin.x + x, origin.y + y, origin.z);

            if (BoardManager.Instance.IsInside(candidate) && !IsOccupied(candidate))
            {
                result.Add(candidate);
            }
        }

        return result;
    }
    private bool IsOccupied(Vector3Int candidate)
    {
        Piece p = BoardManager.Instance.GetPiece(candidate);
        if (p == null)
            return false;
        return p.Color == target.Color;
    }

}
