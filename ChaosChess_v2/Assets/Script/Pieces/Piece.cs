using System.Collections.Generic;
using UnityEngine;

public enum PieceColor
{
    White,
    Black
}

public abstract class Piece : MonoBehaviour
{
    [SerializeField] private PieceColor color;
    [SerializeField] private Vector3Int boardPos;

    public PieceColor Color
    {
        get { return color; }
        set { color = value; }
    }

    public Vector3Int BoardPos
    {
        get { return boardPos; }
        set { boardPos = value; }
    }

    public abstract List<Vector3Int> GetLegalMoves(BoardManager board);
}
