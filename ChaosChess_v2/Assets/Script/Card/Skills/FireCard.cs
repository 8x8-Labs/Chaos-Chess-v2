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
        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(tilePos, DataSO.EffectTileBase);

        boardManager.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.ClearTileEffect(tilePos);

        Destroy(gameObject);
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
    }

    private void OnDestroy()
    {
        boardManager.UnregisterTileEffector(tilePos, this);
    }
}