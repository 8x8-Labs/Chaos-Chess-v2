using UnityEngine;

/// <summary>
/// 평화 협정 공간 - 타일 전용 (고급)
/// 선택한 칸에서 공격당할 시 공격을 1회 취소하고 칸 효과를 해제합니다.
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