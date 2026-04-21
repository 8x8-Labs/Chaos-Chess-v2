using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 풍차 - 기물 전용 (고급)
/// 2턴 동안 룩은 비숍의 행마법대로, 비숍은 룩의 행마법대로 움직입니다.
/// </summary>
public class WindmillCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        var effector = CreateGlobalEffector<WindmillEffector>();
        foreach(Piece piece in BoardManager.Instance.GetAllPieces())
        {
            piece.MoveFenOverride = piece.Type == PieceType.Rook ? "b" : piece.Type == PieceType.Bishop ? "r" : null;
            GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, effector.Revert);
        }
        effector.Apply();

    }
}
public class WindmillEffector : GlobalEffector
{
    protected override void OnApply()
    {
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        foreach (Piece piece in BoardManager.Instance.GetAllPieces())
        {
            if (piece.Type == PieceType.Bishop || piece.Type == PieceType.Rook)
                piece.MoveFenOverride = null;
        }
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}
