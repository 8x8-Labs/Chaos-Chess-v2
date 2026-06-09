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

    [Header("직전 수 하이라이트 (전용 레이어)")]
    [SerializeField] private Tilemap LastMoveBoard;
    [SerializeField] private TileBase LastMoveTile;

    private Vector3Int prvMouseCellPos;
    private List<Vector3Int> drawnMoveTilePositions = new List<Vector3Int>();
    private readonly List<Vector3Int> lastMoveTilePositions = new List<Vector3Int>();

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

    /// <summary>직전 수의 출발/도착 칸을 전용 레이어에 하이라이트합니다.
    /// 좌표가 유효하지 않으면(예: -1) 해당 칸은 칠하지 않으므로, (-1,-1)을 넘기면 클리어만 수행됩니다.</summary>
    public void DrawLastMove(Vector3Int from, Vector3Int to)
    {
        ClearLastMove();
        StampLastMove(from);
        StampLastMove(to);
    }

    private void StampLastMove(Vector3Int pos)
    {
        if (LastMoveBoard == null || LastMoveTile == null) return;
        if (pos.x < 0 || pos.y < 0) return;

        LastMoveBoard.SetTile(pos, LastMoveTile);
        LastMoveBoard.SetTileFlags(pos, TileFlags.None);
        lastMoveTilePositions.Add(pos);
    }

    public void ClearLastMove()
    {
        if (LastMoveBoard != null)
        {
            foreach (Vector3Int pos in lastMoveTilePositions)
                LastMoveBoard.SetTile(pos, null);
        }
        lastMoveTilePositions.Clear();
    }
}
