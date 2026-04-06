using UnityEngine;
using UnityEngine.Tilemaps;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Tilemap ChessBoardTileMap;


    void Update()
    {
        if (!GameManager.Instance.IsGameInput)
            return;
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int mouseGridPos = ChessBoardTileMap.WorldToCell(mouseWorldPos);

            GameManager.Instance.SelectGrid(mouseGridPos);
        }
    }
}
