using UnityEngine;
using UnityEngine.Tilemaps;

public class BoardUI : MonoBehaviour
{
    [SerializeField] private Tilemap UIChessBoard;
    [SerializeField] private TileBase SelectTile;

    private Vector3Int prvMouseCellPos;

    public void DrawSelectTile(Vector3Int pos)
    {
        DeleteSelectTile();

        prvMouseCellPos = pos;

        UIChessBoard.SetTile(pos, SelectTile);
    }
    
    public void DeleteSelectTile()
    {
        UIChessBoard.SetTile(prvMouseCellPos, null); // 전에 선택한 좌표에 선택 ui 지우기
    }
}
