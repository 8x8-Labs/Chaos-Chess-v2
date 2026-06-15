using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 윤회 - 기물 전용 (고급)
/// 프로모션한 상대 폰의 모습과 위치를 되돌립니다.
/// </summary>
public class TransmigrationCard : CardData, IPieceCard, IPieceTargetFilter
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

    public bool CanSelectPiece(Piece piece)
    {
        return piece != null && piece.IsPromotioned;
    }

    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];
        if (!piece.IsPromotioned) return;

        Vector3Int startPos = piece.StartPos;
        PieceColor color = piece.Color;

        BoardManager.Instance.ForceTeleport(piece, startPos);
        BoardManager.Instance.ChangePiece(startPos, color, 'p', piece);
        BoardManager.Instance.RefreshMoves();
    }
}
