using UnityEngine;

/// <summary>
/// 대전차지뢰 - 타일 전용 (희귀)
/// 선택한 칸에 대전차지뢰를 설치합니다. 
/// 이후 룩 이상의 기물가치를 가진 기물의 이동경로에 지뢰가 있으면 반경 한칸내 모든 기물이 죽습니다.
/// </summary>

public class ATMineCard : CardData, ITileCard
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
        Vector3Int pos = args.TargetPos[0];

        ATMineEffector effect = CreateTileEffector<ATMineEffector>(pos);
        effect.Apply();
    }
}

public class ATMineEffector : TileEffector, IPiecePathEffect
{
    private const PieceType TargetPieceTypes =
        PieceType.Rook | PieceType.Queen | PieceType.King |
        PieceType.Amazon | PieceType.Chancellor | PieceType.KnightRider;

    protected override void OnApply()
    {
        ShowTileEffect();
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        ClearTileEffect();
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public void OnPieceTraverse(Piece piece, Vector3Int from, Vector3Int to)
    {
        if ((TargetPieceTypes & piece.Type) == 0) return;
        Explode();
    }

    private void Explode()
    {
        bool pieceDestroyed = false;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int checkPos = tilePos + new Vector3Int(x, y, 0);

                Piece target = BoardManager.Instance.GetPiece(checkPos);
                if (target == null) continue;
                BoardManager.Instance.DestroyPiece(target, false);
                pieceDestroyed = true;
            }
        }

        if (pieceDestroyed)
            BoardManager.Instance.RefreshMoves();

        Revert();
    }
}
