using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정신 집중 - 기물 전용 (전설, 아마존)
/// 선택 기물은 일정 수치 동안 상호작용할 수 없고, 이후 아마존으로 승격됩니다.
/// </summary>
class ConcentrationEffector : PieceEffector
{
    Piece piece;
    public void Init(Piece piece)
    {
        this.piece = piece;
    }
    protected override void OnApply()
    {
        piece.FenOverride = "a";
    }

    protected override void OnRevert()
    {
        if (piece)
            BoardManager.Instance.ChangePiece(piece.Pos, piece.Color, 's');
        Destroy(this);
    }
}
public class ConcentrationCard : CardData, IPieceCard
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

        var effector = CreatePieceEffector<ConcentrationEffector>(piece);
        effector.Init(piece);
        effector.Apply();
        GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, effector.Revert);
    }
}
