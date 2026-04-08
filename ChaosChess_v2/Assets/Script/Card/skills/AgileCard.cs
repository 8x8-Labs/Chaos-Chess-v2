using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 민첩 - 기물 전용 (일반)
/// 폰은 앞이나 뒤로 이동하여 기물을 한 번 잡을 수 있습니다.
/// </summary>
public class AgileCard : CardData, IPieceCard
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
        Piece pawn = args.Targets[0];
        AgileEffect effect = CreatePieceEffector<AgileEffect>(pawn);

        effect.Apply();
    }
}

public class AgileEffect : PieceEffector
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "u";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        Revert();
    }
}