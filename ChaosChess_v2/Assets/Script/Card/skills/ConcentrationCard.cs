
/// <summary>
/// 정신 집중 - 기물 전용 (전설, 아마존)
/// 선택 기물은 일정 수치 동안 상호작용할 수 없고, 이후 아마존으로 승격됩니다.
/// </summary>
class ConcentrationEffector : PieceEffector, IMovementOverrideEffect
{
    protected override void OnApply()
    {
        if (target == null) return;

        target.MoveFenOverride = "a";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        if (target != null)
        {
            if (target.MoveFenOverride?.ToLower() == "a")
                target.MoveFenOverride = null;
            BoardManager.Instance.ChangePiece(target.Pos, target.Color, 's');
        }

        BoardManager.Instance.RefreshMoves();
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
        effector.Apply();
    }
}
