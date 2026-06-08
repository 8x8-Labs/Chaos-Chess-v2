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

        effect.Apply();
    }
}

public class PsilocybinMushroomTileEffect : TileEffector
{
    public CardDataSO DataSO;

    protected override void OnApply()
    {
        ShowTileEffect(DataSO);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        ClearTileEffect();

        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
    }

    public override void OnPieceEnter(Piece piece)
    {
        if (piece == null) return;
        if (PieceEffector.HasActiveMovementOverride(piece)) return;

        var effect = piece.gameObject.AddComponent<PsilocybinMushroomPieceEffect>();
        effect.CardSO = null;
        effect.Init(piece, 3);
        effect.Apply();

        Revert();
    }
}

public class PsilocybinMushroomPieceEffect : PieceEffector, IMovementOverrideEffect
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "w";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        if (target != null && target.MoveFenOverride?.ToLower() == "w")
            target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();

        Destroy(this);
    }
}
