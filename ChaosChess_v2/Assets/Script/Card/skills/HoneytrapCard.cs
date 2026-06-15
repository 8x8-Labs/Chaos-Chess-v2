using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 미인계 - 기물 조건 즉시 발동
/// 카드 사용 시 상대 킹을 퀸 방향으로 1칸 강제 이동시킵니다.
/// </summary>
public class HoneytrapCard : CardData, IPieceCard
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
        List<Piece> pieces = BoardManager.Instance.GetAllPieces();
        List<Piece> queens = new();
        Piece king = null;
        PieceColor pcolor = GameManager.Instance.turnColor;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Type == PieceType.Queen && pieces[i].Color == pcolor)
                queens.Add(pieces[i]);
            else if (pieces[i].Type == PieceType.King && pieces[i].Color != pcolor)
                king = pieces[i];
        }
        if (queens.Count == 0 || king == null)
            return;

        Piece queen = queens[UnityEngine.Random.Range(0, queens.Count) ];

        Vector3Int destination = GetDestination(king.Pos, queen.Pos);
        if (!BoardManager.Instance.IsInside(destination))
            return;

        BoardManager.Instance.ForceTeleport(king, destination);

        // 상대 킹에게 미인계 연출 재생 (SO의 VFX.ApplyVFXPrefab 사용)
        if (DataSO != null)
        {
            VFXSpawner.SpawnOneShot(DataSO.VFX.ApplyVFXPrefab, king.transform.position, king.transform);
            if (DataSO.VFX.PlayApplyAnim)
                VFXSpawner.PlayPunch(king.transform, DataSO.VFX.AnimStrength, DataSO.VFX.AnimDuration);
        }
    }

    private Vector3Int GetDestination(Vector3Int kingPos, Vector3Int queenPos)
    {
        int deltaX = queenPos.x - kingPos.x;
        int deltaY = queenPos.y - kingPos.y;
        int stepX = Mathf.Clamp(deltaX, -1, 1);
        int stepY = Mathf.Clamp(deltaY, -1, 1);

        List<Vector3Int> directions = new();

        if (stepX != 0 && stepY != 0)
        {
            directions.Add(new Vector3Int(stepX, stepY, 0));

            bool xFirst = Mathf.Abs(deltaX) > Mathf.Abs(deltaY)
                || (Mathf.Abs(deltaX) == Mathf.Abs(deltaY) && Random.value < 0.5f);
            directions.Add(xFirst
                ? new Vector3Int(stepX, 0, 0)
                : new Vector3Int(0, stepY, 0));
            directions.Add(xFirst
                ? new Vector3Int(0, stepY, 0)
                : new Vector3Int(stepX, 0, 0));
        }
        else if (stepY != 0)
        {
            directions.Add(new Vector3Int(0, stepY, 0));
            AddRandomizedPair(
                directions,
                new Vector3Int(-1, stepY, 0),
                new Vector3Int(1, stepY, 0));
        }
        else if (stepX != 0)
        {
            directions.Add(new Vector3Int(stepX, 0, 0));
            AddRandomizedPair(
                directions,
                new Vector3Int(stepX, -1, 0),
                new Vector3Int(stepX, 1, 0));
        }

        foreach (Vector3Int direction in directions)
        {
            Vector3Int candidate = kingPos + direction;
            if (BoardManager.Instance.IsInside(candidate))
                return candidate;
        }

        return new Vector3Int(-1, -1, 0);
    }

    private void AddRandomizedPair(List<Vector3Int> directions, Vector3Int first, Vector3Int second)
    {
        if (Random.value < 0.5f)
        {
            directions.Add(first);
            directions.Add(second);
        }
        else
        {
            directions.Add(second);
            directions.Add(first);
        }
    }
}
