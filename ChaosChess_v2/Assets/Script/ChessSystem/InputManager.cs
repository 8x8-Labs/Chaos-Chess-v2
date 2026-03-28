using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Tilemap ChessBoardTileMap;

    private GameManager gameManager;

    void Awake()
    {
        gameManager = GetComponent<GameManager>();
    }

    void Update()
    {
        if (gameManager.IsWaitingPromotion)
            return;
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int mouseGridPos = ChessBoardTileMap.WorldToCell(mouseWorldPos);

            gameManager.SelectGrid(mouseGridPos);
        }

    }
}
