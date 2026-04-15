using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 미인계 - 전역
/// 카드 사용 시 상대 킹을 퀸과의 맨해튼 거리가 줄어드는 방향으로 이동시킵니다.
/// </summary>
public class HoneytrapCard : CardData, ICard
{
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
        if (queens.Count == 0)
            return;
        Piece queen = queens[UnityEngine.Random.Range(0, queens.Count) ];
        int[] dx = { -1, -1, -1, 0, 1, 1, 1, 0 };
        int[] dy = { -1, 0, 1, 1, 1, 0, -1, -1 };
        int minv = 1000;
        Vector3Int k = king.Pos;
        Vector3Int ans = new();
        for (int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];
            Vector3Int can = new Vector3Int(k.x + x, k.y + y, k.z);
            if (BoardManager.Instance.IsInside(can) && !IsOccupied(can))
            {
                int dist = MDist(can, queen.Pos);

                if (dist < minv)
                {
                    minv = dist;
                    ans = can;
                }
            }
        }
        if (minv == 1000)
            return;
        Debug.Log(ans);
        Debug.Log(king.Pos);
        BoardManager.Instance.ForceTeleport(king, ans);
    }
    private int MDist(Vector3Int a, Vector3Int b)
    {
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }
    private bool IsOccupied(Vector3Int candidate)
    {
        Piece p = BoardManager.Instance.GetPiece(candidate);
        return p != null;
    }
}
