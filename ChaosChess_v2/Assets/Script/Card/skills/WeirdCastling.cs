using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기묘한 캐슬링 - 기물전용
/// 턴을 소모해서 킹 기물이 선택한 기물과 위치를 교환한다.
/// </summary>
public class WeirdCastling : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    [ContextMenu("Execute")]
    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();

        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Debug.Log($"카드 실행! : {DataSO.CardName}");
        Piece targetPiece = args.Targets[0];
        Piece king = BoardManager.Instance.GetPiece<King>(DataSO.PieceTargetColor)[0];

        List<Piece> pieces = new List<Piece> { king, targetPiece };
        List<Vector3Int> newPositions = new List<Vector3Int> { targetPiece.Pos, king.Pos };
        BoardManager.Instance.BatchReassign(pieces, newPositions);

        GameManager.Instance.NextTurn();
        GameManager.Instance.RequestAIMove();
    }
}