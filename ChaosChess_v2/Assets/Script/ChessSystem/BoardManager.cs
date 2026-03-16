using System;
using UnityEngine;


public class BoardManager : MonoBehaviour
{
    [SerializeField] private GameObject[] StartPieces;
    private Piece[,] board = new Piece[8, 8];

    void Start()
    {
        foreach (GameObject StartPiece in StartPieces)
        {
            Piece piece = StartPiece.GetComponent<Piece>();
            AddPiece(piece, piece.BoardPos);
        }
    }

    private void AddPiece(Piece piece, Vector3Int pos)
    {
        board[pos.x, pos.y] = piece;
    }

    private bool IsInside(Vector3Int pos)
    {
        return pos.x >= 0 && pos.x < 8 && pos.y >= 0 && pos.y < 8;
    }

    public bool IsEmpty(Vector3Int pos)
    {
        if (!IsInside(pos)) return false;

        return board[pos.x, pos.y] == null;
    }

    public Piece GetPiece(Vector3Int pos)
    {
        if (!IsInside(pos)) return null;

        return board[pos.x, pos.y];
    }

    public void MovePiece(Piece piece, Vector3Int target)
    {
        board[piece.BoardPos.x, piece.BoardPos.y] = null;

        piece.BoardPos = target;

        Vector3 pos = new Vector3((target.x - 3.5f) * 0.65f, (target.y - 3.5f) * 0.65f, 0);
        piece.transform.position = pos;

        board[target.x, target.y] = piece;
    }
}
