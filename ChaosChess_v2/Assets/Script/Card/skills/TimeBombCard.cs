using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시한폭탄 - 타일 전용 (레어)
/// 3턴 후 지정된 칸과 주변 4칸의 기물이 죽습니다.
/// 기물이 없는 칸만 선택할 수 있습니다.
/// </summary>
public class TimeBombCard : CardData, ITileCard
{
    private TileSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    [ContextMenu("Execute")]
    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        List<Vector3Int> tiles = args.TargetPos;
        // TODO: 선택된 빈 칸에 시한폭탄 배치
        //       DataSO.MaintainTurn(3)턴 후 해당 칸과 상하좌우 4칸의 기물을 모두 파괴 처리

        Vector3Int center = args.TargetPos[0];

        GameManager.Instance.AppendAction(DataSO.MaintainTurn, () =>
        {
            Explode(center);
        });
    }

    private void Explode(Vector3Int center)
    {
        Vector3Int[] dirs = new Vector3Int[]
        {
        Vector3Int.zero,
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
        };

        foreach (var dir in dirs)
        {
            Vector3Int pos = center + dir;

            Piece piece = BoardManager.Instance.GetPiece(pos);
            if (piece != null)
            {
                BoardManager.Instance.DestroyPiece(piece);
            }
        }

        BoardManager.Instance.RefreshMoves();
    }
}
