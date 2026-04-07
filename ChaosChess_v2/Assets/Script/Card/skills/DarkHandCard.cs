using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어둠 속의 손 - 기물 전용 (희귀)
/// 나이트, 룩, 비숍, 폰에 적용 가능. 2턴간 움직일 수 없도록 고정시킵니다.
/// </summary>
public class DarkHandCard : CardData, IPieceCard
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
        var effector = CreatePieceEffector<DarkHandEffector>(piece);
        effector.Apply();
        GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, effector.Revert);
    }
}

public class DarkHandEffector : PieceEffector
{
    protected override void OnApply()
    {
        target.FenOverride = "a";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        target.FenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}
