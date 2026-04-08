using UnityEngine;

/// <summary>
/// 평화 협정 공간 - 타일 전용 (일반)
/// 이 칸을 밟은 기물은 3턴동안 킹의 행마법을 가집니다.
/// </summary>
public class PeaceZoneCard : CardData, ITileCard
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

        PeaceZoneCardEffect effect = CreateTileEffector<PeaceZoneCardEffect>(tile);
        effect.Apply();
    }
}

public class PeaceZoneCardEffect : TileEffector
{
    protected override void OnApply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override bool CanPieceEnter(Piece piece, Vector3Int from, Vector3Int to)
    {
        Piece targetPiece = BoardManager.Instance.GetPiece(to);
        if (targetPiece != null)
        {
            Revert();
            return false;
        }
        return true;
    }
}