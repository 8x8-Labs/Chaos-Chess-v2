using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 풍차 - 기물 전용 (고급)
/// 2턴 동안 룩은 비숍의 행마법대로, 비숍은 룩의 행마법대로 움직입니다.
/// </summary>
public class WindmillCard : CardData, IPieceCard
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
        var effector = CreatePieceEffector<WindmillEffector>(piece);
        effector.change = piece.Type == PieceType.Rook ? "b" : "r";
        effector.Apply();
        GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, effector.Revert);

    }
}
public class WindmillEffector : PieceEffector
{
    public string change;
    public override void Apply()
    {
        target.FenOverride = change;
        BoardManager.Instance.RefreshMoves();
    }

    public override void Revert()
    {
        target.FenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }

}
