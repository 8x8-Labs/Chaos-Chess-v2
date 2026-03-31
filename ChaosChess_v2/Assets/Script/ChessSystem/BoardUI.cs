using UnityEngine;
using DG.Tweening;
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
        UIChessBoard.SetTileFlags(pos, TileFlags.None);

        Matrix4x4 startMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.zero);
        UIChessBoard.SetTransformMatrix(pos, startMatrix);

        DOTween.To(() => 0f, val =>
        {
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(val, val, 1));
            UIChessBoard.SetTransformMatrix(pos, matrix);
        }, 1f, 0.15f).SetEase(Ease.OutBack); // 살짝 튕기는 효과

    }
    
    public void DeleteSelectTile()
    {
        UIChessBoard.SetTile(prvMouseCellPos, null); // 전에 선택한 좌표에 선택 ui 지우기
    }
}
