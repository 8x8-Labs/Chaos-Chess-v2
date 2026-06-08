using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성 위의 말 - 기물 전용 (챈슬러 합체)
/// 턴 소모 없이 나이트를 선택함
/// 나이트가 가장 가까운 룩 자리로 이동해 합쳐져 챈슬러로 승격 (나이트가 있던 자리에는 기물이 남지 않음).
/// </summary>
public class CastleKnightCard : CardData, IPieceCard
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
        
        Piece knight = args.Targets[0];
        Vector3Int p = knight.Pos;

        List<Piece> pieces = BoardManager.Instance.GetAllPieces();
        Vector3Int pos = Vector3Int.zero;
        int minSqrDist = int.MaxValue;
        bool found = false;
        foreach (Piece piece in pieces)
        {
            if (piece.Type == PieceType.Rook)
            {
                int sqrDist = (p - piece.Pos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    pos = piece.Pos;
                    found = true;
                }
            }
        }

        if (!found)
            return;
        BoardManager.Instance.ChangePiece(pos, knight.Color, 'y');
        BoardManager.Instance.DestroyPiece(knight);
    }
}
