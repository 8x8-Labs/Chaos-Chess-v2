using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성벽 - 타일 전용 (일반)
/// 벽을 생성하여 기물이 그 벽을 넘어갈 수 없도록 하며, 공격 시 벽이 부서질 수 있습니다.
/// </summary>
public class RampartCard : CardData, ITileCard
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
        List<Vector3Int> tiles = args.TargetPos;
        PieceColor color = GameManager.Instance.turnColor;
        foreach (Vector3Int pos in tiles)
        {
            BoardManager.Instance.ChangePiece(pos, color, 'a');
        }
    }
}
