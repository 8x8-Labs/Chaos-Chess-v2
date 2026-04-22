using UnityEngine;
using UnityEngine.Tilemaps;

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
}
