using UnityEngine;
using UnityEngine.Tilemaps;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Tilemap ChessBoardTileMap;

    private BoardManager boardManager;
    private BoardUI boardUI;
    private GamaManager gamaManager;

    void Start()
    {
        boardManager = GetComponent<BoardManager>();
        boardUI = GetComponent<BoardUI>();
        gamaManager = GetComponent<GamaManager>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int mouseCellPos = ChessBoardTileMap.WorldToCell(mouseWorldPos);
            Debug.Log(mouseCellPos);
            Piece piece = boardManager.GetPiece(mouseCellPos);

            if (piece != null)
            {
                gamaManager.SelectPiece(piece);
                boardUI.DrawSelectTile(mouseCellPos);
            }
            else
            {
                gamaManager.MoveSelected(mouseCellPos, boardManager);
                boardUI.DeleteSelectTile();
            }
        }
    }
}
