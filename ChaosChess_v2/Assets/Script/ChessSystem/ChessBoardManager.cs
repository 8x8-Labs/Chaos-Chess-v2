using UnityEngine;
using UnityEngine.Tilemaps;

public class ChessBoardManager : MonoBehaviour
{
    [SerializeField] private Tilemap UIChessBoard;
    [SerializeField] private TileBase SelectTile;

    private Vector3Int mouseCellPos;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            UIChessBoard.SetTile(mouseCellPos, null);// 전에 선택한 좌표에 선택 ui 지우기

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseCellPos = UIChessBoard.WorldToCell(mouseWorldPos);
            UIChessBoard.SetTile(mouseCellPos, SelectTile);
        }
    }
}
