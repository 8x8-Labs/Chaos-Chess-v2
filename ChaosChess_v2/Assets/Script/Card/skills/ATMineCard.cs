using System.Collections.Generic;
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

        ATMineEffector effect = CreateGlobalEffector<ATMineEffector>();
        effect.MinePos = pos;

        PieceType targetPiecesType = (
            PieceType.Rook | PieceType.Queen | PieceType.King | // 일반 기물
            PieceType.Amazon | PieceType.Chancellor | PieceType.KnightRider // 커스텀 기물
        );

        effect.DataSO = DataSO;
        effect.Init(targetPiecesType);
        effect.Apply();
    }
}

public class ATMineEffector : GlobalEffector
{
    public CardDataSO DataSO;

    public Vector3Int MinePos;
    protected override void OnApply()
    {
        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(MinePos, DataSO.EffectTileBase);

        BoardManager.Instance.RegisterGlobalEffector(this);
    }

    protected override void OnRevert()
    {
        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.ClearTileEffect(MinePos);

        BoardManager.Instance.UnregisterGlobalEffector(this);
        Destroy(gameObject);
    }

    public override void OnPieceAct(Piece piece, Vector3Int dest)
    {
        if (!IsWatching(piece)) return;

        Vector3Int from = piece.PrevPos;
        Vector3Int to = dest;

        int dx = to.x - from.x;
        int dy = to.y - from.y;

        int adx = Mathf.Abs(dx);
        int ady = Mathf.Abs(dy);

        Vector3Int dir;

        if (dx == 0 || dy == 0 || adx == ady)
        {
            dir = new Vector3Int(
                Mathf.Clamp(dx, -1, 1),
                Mathf.Clamp(dy, -1, 1),
                0
            );
        }
        else if (adx != ady && dx != 0 && dy != 0)
        {
            if (adx > ady)
            {
                dir = new Vector3Int(
                    dx / adx * 2,
                    dy / ady * 1,
                    0
                );
            }
            else
            {
                dir = new Vector3Int(
                    dx / adx * 1,
                    dy / ady * 2,
                    0
                );
            }
        }
        else
        {
            return;
        }

        Vector3Int curPos = from;

        while (curPos != to)
        {
            curPos += dir;

            if (curPos == MinePos)
            {
                Explode();
                return;
            }
        }
    }

    private void Explode()
    {
        bool pieceDestroyed = false;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int checkPos = MinePos + new Vector3Int(x, y, 0);

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

    protected override IEnumerable<Vector3Int> GetVisualPositions()
    {
        yield return MinePos;
    }
}
