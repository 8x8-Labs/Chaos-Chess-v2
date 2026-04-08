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
        effect.Apply();
    }
}

public class FireEffect : TileEffector
{
    private BoardManager boardManager = BoardManager.Instance;
    private Piece enterPiece;
    private int deathTurn = 0;

    protected override void OnApply()
    {
        boardManager.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        boardManager.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        if(enterPiece == null)
            enterPiece = piece;
    }

    public override void OnPieceExit(Piece piece) => Revert();

    public override void OnTurnChanged()
    {
        if(enterPiece != null)
        {
            deathTurn++;

            if(deathTurn > 1)
            {
                boardManager.DestroyPiece(enterPiece);
                Revert();
            }
        }
    }

    private void OnDestroy() => Revert();
}