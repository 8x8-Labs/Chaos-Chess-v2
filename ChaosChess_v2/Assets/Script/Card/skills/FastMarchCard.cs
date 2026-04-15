/// <summary>
/// 신속행군 - 기물 전용 (일반)
/// 폰 기물이 전방으로 2칸 전진할 수 있게 된다.
/// </summary>
public class FastMarchCard : CardData, IPieceCard
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
        FastMarchCardEffector effector = CreatePieceEffector<FastMarchCardEffector>(piece);

        effector.Apply(true);
    }
}

public class FastMarchCardEffector : PieceEffector
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "q";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();

        Destroy(this);
    }
    protected override void OnHalfTurnChanged()
    {
        Revert();
    }
}