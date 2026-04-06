public class MissingPromotionCard : CardData, IPieceCard
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
        if (args.Targets == null || args.Targets.Count == 0) return; 
        Piece p = args.Targets[0];

        BoardManager.Instance.ChangePiece(p.Pos, PieceColor.Black, 'p');
    }

}