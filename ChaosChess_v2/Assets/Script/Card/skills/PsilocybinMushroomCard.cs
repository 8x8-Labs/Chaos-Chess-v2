using UnityEngine;

/// <summary>
/// 환각 버섯 - 타일 전용 (일반)
/// 이 칸을 밟은 기물은 3턴동안 킹의 행마법을 가집니다.
/// </summary>
public class PsilocybinMushroomCard : CardData, ITileCard
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
        Vector3Int tile = args.TargetPos[0];

        PsilocybinMushroomTileEffect effect = CreateTileEffector<PsilocybinMushroomTileEffect>(tile);

        effect.DataSO = DataSO;

        effect.SetOwner(this);
        effect.Apply();
    }

    public void ApplyPieceEffect(Piece piece)
    {
        if (piece == null) return;

        var effect = CreatePieceEffector<PsilocybinMushroomPieceEffect>(piece);
        effect.Init(piece, 3);
        effect.Apply();
    }
}

public class PsilocybinMushroomTileEffect : TileEffector
{
    public CardDataSO DataSO;

    private PsilocybinMushroomCard owner;

    public void SetOwner(PsilocybinMushroomCard card)
    {
        owner = card;
    }

    protected override void OnApply()
    {
        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(tilePos, DataSO.EffectTileBase);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.ClearTileEffect(tilePos);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
    }

    public override void OnPieceEnter(Piece piece)
    {
        if (piece == null || owner == null) return;

        owner.ApplyPieceEffect(piece);

        Revert();
    }
}

public class PsilocybinMushroomPieceEffect : PieceEffector
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "w";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();

        Destroy(this);
    }
}