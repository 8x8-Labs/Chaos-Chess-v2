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
    [SerializeField] protected PieceType type;
    protected SpriteRenderer spriteRenderer;

    [SerializeField] private PieceColor color;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Vector3Int pos;

    protected List<Vector3Int> CanMovePos;

    private MaterialPropertyBlock mpb;
    private static readonly int OutlineThickId = Shader.PropertyToID("_OutlineThick");

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

    public PieceType Type
    {
        get { return type; }
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sharedMaterial = outlineMaterial;
        mpb = new MaterialPropertyBlock();
    }

    public virtual void Init(Vector3Int pos, PieceColor color, Sprite white, Sprite black)
    {
        Color = color;
        Pos = pos;

        if (Color == PieceColor.White)
            spriteRenderer.sprite = white;
        else
            spriteRenderer.sprite = black;
    }
    public virtual void Init(Vector3Int pos, PieceColor color)
    {
        Init(pos, color, WhitePiece, BlackPiece);
    }
    public virtual bool CanMoveTo(BoardManager board, Vector3Int target)
    {
        foreach (Vector3Int pos in CanMovePos)
        {
            if (target == pos)
            {
                return true;
            }
        }
        return false;
    }

    public void ResetCanMovePos()
    {
        CanMovePos = new List<Vector3Int>();
    }

    public void AddCanMovePos(Vector3Int pos)
    {
        CanMovePos.Add(pos);
    }

    public virtual void Move(Vector3Int target, Vector3 WorldPos)
    {
        Pos = target;

        transform.position = WorldPos;
    }

    public void OnSelected()
    {
        spriteRenderer.sortingLayerName = "SelectTarget";
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(OutlineThickId, 1f);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    public void OnDeselect()
    {
        spriteRenderer.sortingLayerName = "Default";
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(OutlineThickId, 0f);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    public virtual string GetFen() { return ""; }
}
