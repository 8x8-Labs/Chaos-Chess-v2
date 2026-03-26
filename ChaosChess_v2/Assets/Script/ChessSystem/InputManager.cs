using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Tilemap ChessBoardTileMap;

    private GamaManager gamaManager;

    void Awake()
    {
        gamaManager = GetComponent<GamaManager>();
    }

    void Update()
    {
        if (gamaManager.IsWaitingPromotion)
            return;
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int mouseGridPos = ChessBoardTileMap.WorldToCell(mouseWorldPos);

            gamaManager.SelectGrid(mouseGridPos);
        }

    }
}
