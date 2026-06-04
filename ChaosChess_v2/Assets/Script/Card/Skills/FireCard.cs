/// <summary>
/// 불바다
/// 현재 타일에 진입한 기물이 2턴 뒤 제거됩니다.
/// </summary>
public class FireCard : CardData, ITileCard
{
    private TileSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();
        selector.EnableSelector(this);
    }
    public void Execute(CardEffectArgs args = null)
    {
        FireEffect effect = CreateTileEffector<FireEffect>(args.TargetPos[0]);
        effect.DataSO = DataSO;
        effect.Apply();
    }
}

public class FireEffect : TileEffector
{
    public CardDataSO DataSO;

    private BoardManager boardManager = BoardManager.Instance;
    private Piece enterPiece;
    private int deathTurn = 0;

    protected override void OnApply()
    {
        Piece.OnPieceDestroyed += HandlePieceDestroyed;

        ShowTileEffect(DataSO);

        boardManager.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        Piece.OnPieceDestroyed -= HandlePieceDestroyed;

        ClearTileEffect();

        Destroy(gameObject);
    }

    private void HandlePieceDestroyed(Piece piece)
    {
        if (piece == enterPiece) { enterPiece = null; Revert(); }
    }

    public override void OnPieceEnter(Piece piece)
    {
        if (enterPiece == null)
            enterPiece = piece;
    }

    public override void OnPieceExit(Piece piece) => Revert();

    public override void OnTurnChanged()
    {
        if (enterPiece != null)
        {
            deathTurn++;

            if (deathTurn > 1)
            {
                boardManager.DestroyPiece(enterPiece);
                Revert();
            }
        }

        RefreshTileEffectTurnAnimation(DataSO);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Piece.OnPieceDestroyed -= HandlePieceDestroyed;
        boardManager.UnregisterTileEffector(tilePos, this);
    }
}
