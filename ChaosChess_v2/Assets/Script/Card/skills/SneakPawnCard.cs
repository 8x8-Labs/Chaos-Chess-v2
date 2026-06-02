/// <summary>
/// 암습의 폰 - 기물 전용 (고급)
/// 폰 기물이 나이트 기물의 이동 방식을 1회 가지게 된다.
/// </summary>
public class SneakPawnCard : CardData, IPieceCard
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
        if (PieceEffector.HasActiveMovementOverride(piece)) return;

        piece.MoveFenOverride = "e";
        BoardManager.Instance.RefreshMoves();

        GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, () =>
        {
            ResetMoveFen(piece);
        });
    }
    public void ResetMoveFen(Piece piece)
    {
        if (piece == null) return;

        string p = piece.MoveFenOverride?.ToLower();
        if (p != "e") return;

        piece.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
    }
}
