using System.Collections.Generic;
using UnityEngine;

public enum PieceColor
{
    White,
    Black
}

public class Piece : MonoBehaviour
{
    [SerializeField] protected Sprite WhitePiece;
    [SerializeField] protected Sprite BlackPiece;
    protected SpriteRenderer spriteRenderer;

    [SerializeField] protected List<Vector2Int> CanMoveOffsets;

    [SerializeField] private PieceColor color;
    [SerializeField] private Vector3Int pos;

    protected List<Vector3Int> CanMovePos;

    public PieceColor Color
    {
        get { return color; }
        set { color = value; }
    }

    public Vector3Int Pos
    {
        get { return pos; }
        set { pos = value; }
    }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (Color == PieceColor.White)
            spriteRenderer.sprite = WhitePiece;
        else
            spriteRenderer.sprite = BlackPiece;
    }

    public virtual bool CanMoveTo(BoardManager board, Vector3Int target)
    {
        foreach (Vector3Int pos in CanMovePos)
        {
            if (target == pos)
            {
                Debug.Log("된다");
                return true;
            }
        }
        Debug.Log("안된다");
        return false;
    }

    public virtual void UpdateCanMovePos(BoardManager board)
    {
        CanMovePos = new List<Vector3Int>();

        foreach (Vector2Int CanMoveOffset in CanMoveOffsets)
        {
            Vector3Int target = pos;
            while (true)
            {
                target = new Vector3Int(target.x + CanMoveOffset.x, target.y + CanMoveOffset.y, 0);
                if (board.IsEmpty(target))
                {
                    CanMovePos.Add(target);
                }
                else
                {
                    break;
                }
            }
        }
    }
}
