using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class UITileEffectDrawer : MonoBehaviour
{
    [SerializeField] private Tilemap effectTilemap;

    public void SetTileEffect(Vector3Int pos, TileBase tile)
    {
        effectTilemap.SetTile(pos, tile);
    }

    public void ClearTileEffect(Vector3Int pos)
    {
        effectTilemap.SetTile(pos, null);
    }

    /// <summary>현재 타일 이펙트 맵을 위치-타일 스냅샷으로 저장합니다.</summary>
    public Dictionary<Vector3Int, TileBase> CaptureTileEffects()
    {
        var snapshot = new Dictionary<Vector3Int, TileBase>();

        foreach (Vector3Int pos in effectTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = effectTilemap.GetTile(pos);
            if (tile != null)
                snapshot[pos] = tile;
        }

        return snapshot;
    }

    /// <summary>저장된 스냅샷 기준으로 타일 이펙트를 복원합니다.</summary>
    public void RestoreTileEffects(Dictionary<Vector3Int, TileBase> snapshot)
    {
        effectTilemap.ClearAllTiles();

        if (snapshot == null)
            return;

        foreach (var pair in snapshot)
        {
            effectTilemap.SetTile(pair.Key, pair.Value);
        }
    }

    /// <summary>타일 이펙트 맵을 전체 초기화합니다.</summary>
    public void ClearAllTileEffects()
    {
        effectTilemap.ClearAllTiles();
    }
}
