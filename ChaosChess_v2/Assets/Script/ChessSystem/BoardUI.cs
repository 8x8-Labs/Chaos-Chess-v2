using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Tilemaps;

public class BoardUI : MonoBehaviour
{
    [SerializeField] private Tilemap UIChessBoard;
    [SerializeField] private TileBase SelectTile;
    [SerializeField] private TileBase MoveTile;
    [SerializeField] private TileBase CaptureTile;

    private Vector3Int prvMouseCellPos;
    private List<Vector3Int> drawnMoveTilePositions = new List<Vector3Int>();

    public void DrawSelectTile(Vector3Int pos)
    {
        DeleteSelectTile();
        DeleteValidMoveTiles();

        prvMouseCellPos = pos;

        UIChessBoard.SetTile(pos, SelectTile);
        UIChessBoard.SetTileFlags(pos, TileFlags.None);

        Matrix4x4 startMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1f));
        UIChessBoard.SetTransformMatrix(pos, startMatrix);

        DOTween.To(() => 0.5f, val =>
        {
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(val, val, 1));
            UIChessBoard.SetTransformMatrix(pos, matrix);
        }, 1f, 0.25f).SetEase(Ease.OutQuint);
    }
    
    public void DeleteSelectTile()
    {
        UIChessBoard.SetTile(prvMouseCellPos, null); // 전에 선택한 좌표에 선택 ui 지우기
    }

    public void DeleteValidMoveTiles()
    {
        foreach (Vector3Int pos in drawnMoveTilePositions)
            UIChessBoard.SetTile(pos, null);
        drawnMoveTilePositions.Clear();
    }

    public void DrawValidMoveTiles(Piece piece)
    {
        DeleteValidMoveTiles();
        if (piece == null) return;

        foreach (Vector3Int pos in piece.CanMovePos)
        {
            TileBase tile;
            Piece occupant = BoardManager.Instance.GetPiece(pos);

            if (occupant == null)
                tile = MoveTile;
            else if (occupant.Color != piece.Color)
                tile = CaptureTile;
            else
                continue;

            UIChessBoard.SetTile(pos, tile);
            UIChessBoard.SetTileFlags(pos, TileFlags.None);

            Matrix4x4 startMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.zero);
            UIChessBoard.SetTransformMatrix(pos, startMatrix);

            Vector3Int capturedPos = pos;
            DOTween.To(() => 0.5f, val =>
            {
                Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(val, val, 1));
                UIChessBoard.SetTransformMatrix(capturedPos, matrix);
            }, 1f, 0.25f).SetEase(Ease.OutQuint);

            drawnMoveTilePositions.Add(pos);
        }
    }
}
