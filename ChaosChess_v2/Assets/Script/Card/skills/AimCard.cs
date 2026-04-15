/// <summary>
/// 조준 - 기물 전용 (일반)
/// 폰 기물이 전방 사선으로 1회 이동할 수 있습니다.
/// </summary>
public class AimCard : CardData, IPieceCard
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
    public void ResetMoveFen(Piece piece)
    {
        piece.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
    }

    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];
        AimEffector effector = CreatePieceEffector<AimEffector>(piece);

        effector.Apply(true);
    }
}

public class AimEffector : PieceEffector
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "t";
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