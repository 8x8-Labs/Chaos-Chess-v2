using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class UITileDrawer : MonoBehaviour
{
    [SerializeField] private Tilemap UIChessBoard;
    [SerializeField] private TileBase SelectTile;

    private List<Vector3Int> positions = new List<Vector3Int>();

    public void DrawSelectTile(Vector3Int pos)
    {
        UIChessBoard.SetTile(pos, SelectTile);
        UIChessBoard.SetTileFlags(pos, TileFlags.None);

        Matrix4x4 startMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.zero);
        UIChessBoard.SetTransformMatrix(pos, startMatrix);

        DOTween.To(() => 0f, val =>
        {
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(val, val, 1));
            UIChessBoard.SetTransformMatrix(pos, matrix);
        }, 1f, 0.15f).SetEase(Ease.OutBack);

        positions.Add(pos);
    }

    public void EraseSelectTile(Vector3Int pos)
    {
        UIChessBoard.SetTileFlags(pos, TileFlags.None);
        UIChessBoard.SetTile(pos, null); // 애니메이션 끝난 후 타일 제거
        positions.Remove(pos);
    }

    public void EraseAllSelectTile()
    {
        foreach (Vector3Int pos in positions)
        {
            UIChessBoard.SetTileFlags(pos, TileFlags.None);
            UIChessBoard.SetTile(pos, null); // 애니메이션 끝난 후 타일 제거
        }

        positions.Clear();
    }
}