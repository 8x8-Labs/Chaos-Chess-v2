using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [SerializeField] private Piece[] Pieces;
    private Piece[,] board = new Piece[8, 8];

    void Start()
    {
        foreach (Piece piece in Pieces)
        {
            AddPiece(piece, piece.Pos);
            piece.transform.position = GridPosToWorldPos(piece.Pos);
        }
        UpdatePiecesCanMovePos();
    }

    public void UpdatePiecesCanMovePos()
    {
        foreach (Piece piece in Pieces)
        {
            piece.UpdateCanMovePos(this);
        }
    }

    private void AddPiece(Piece piece, Vector3Int pos) // board에 기물 추가하기
    {
        board[pos.x, pos.y] = piece;
    }

    public bool IsInside(Vector3Int pos) // 선택한 좌표가 체스판 안에 있는가
    {
        return pos.x >= 0 && pos.x < 8 && pos.y >= 0 && pos.y < 8;
    }

    public bool IsEmpty(Vector3Int pos) // 선택한 좌표에 기물이 있는가
    {
        if (!IsInside(pos)) return false;

        return board[pos.x, pos.y] == null;
    }

    public Piece GetPiece(Vector3Int pos) // 선택한 좌표에 기물을 가져온다
    {
        if (!IsInside(pos)) return null;

        return board[pos.x, pos.y];
    }

    public bool MovePiece(Piece piece, Vector3Int target) // 기물의 위치를 target으로 이동시킨다
    {
        if (!piece.CanMoveTo(this, target)) // target으로 이동 가능한가
            return false;

        board[piece.Pos.x, piece.Pos.y] = null;

        piece.Pos = target;

        piece.transform.position = GridPosToWorldPos(target);

        board[target.x, target.y] = piece;

        return true;
    }

    public Vector3 GridPosToWorldPos(Vector3Int GridPos)
    {
        return new Vector3((GridPos.x - 3.5f) * 0.65f, (GridPos.y - 3.5f) * 0.65f, 0);
    }
}
