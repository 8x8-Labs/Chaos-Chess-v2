/// <summary>
/// 불바다
/// 현재 타일에 진입한 기물이 다음 턴에 제거됩니다.
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

    public override void OnPieceExit(Piece piece)
    {
        // 턴 종료 전에 기물이 타일을 벗어나면 파괴 대상에서 제외한다.
        if (enterPiece == piece)
            enterPiece = null;
    }

    public override void OnTurnChanged()
    {
        if (enterPiece == null)
            return;

        Piece doomedPiece = enterPiece;
        enterPiece = null;
        boardManager.DestroyPiece(doomedPiece);
        Revert();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Piece.OnPieceDestroyed -= HandlePieceDestroyed;
        boardManager.UnregisterTileEffector(tilePos, this);
    }
}
