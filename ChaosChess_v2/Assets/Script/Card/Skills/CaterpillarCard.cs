using UnityEngine;

/// <summary>
/// 무한궤도 - 기물 전용 (고급)
/// 나이트가 2턴 동안 나이트라이더의 행마법대로 이동할 수 있습니다.
/// </summary>
public class CaterpillarCard : CardData, IPieceCard
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
        var effector = CreatePieceEffector<CaterpillarEffector>(args.Targets[0]);
        effector.Apply();
        GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, effector.Revert);
    }
}

public class CaterpillarEffector : PieceEffector
{
    public override void Apply()
    {
        target.FenOverride = "z";
        RefreshMoves();
    }

    public override void Revert()
    {
        target.FenOverride = null;
        RefreshMoves();
        Destroy(this);
    }

    private void RefreshMoves()
    {
        BoardManager bm = BoardManager.Instance;
        bm.UpdateFEN();
        string fen = bm.GetFEN();
        FairyStockfishBridge.Instance.SetPosition(fen);
        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        bm.UpdatePiecesCanMovePos(moves);
    }
}
