using System;
using System.Collections.Generic;
using UnityEngine;

public enum PieceColor
{
    White,
    Black
}

public class Piece : MonoBehaviour
{
    // 무언갈 잡을때 효과 발동
    private List<Action<Vector3Int>> onCaptureEffects = new();

    [SerializeField] protected Sprite WhitePiece;
    [SerializeField] protected Sprite BlackPiece;
    [SerializeField] protected PieceType type;
    protected SpriteRenderer spriteRenderer;

    [SerializeField] private PieceColor color;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Vector3Int pos;
    private string _fenOverride;

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
    /// <summary>
    /// 효과를 통해 변경된 기물의 FEN을 저장합니다.
    /// </summary>
    public string FenOverride
    {
        get
        {
            if (_fenOverride == null) return null;
            return Color == PieceColor.White
                ? _fenOverride.ToUpper()
                : _fenOverride.ToLower();
        }
        set
        {
            if(value == null) _fenOverride = null;
            else
            {
                string v =
                    Color == PieceColor.White
                    ? value.ToUpper()
                    : value.ToLower();
                _fenOverride = v;
            }
        }
    }


    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sharedMaterial = outlineMaterial;
        mpb = new MaterialPropertyBlock();
    }

    public virtual void Init(Vector3Int pos, PieceColor color)
    {
        Color = color;
        Pos = pos;

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

    /// <summary>
    /// 기물을 잡았을 때 호출됩니다.
    /// </summary>
    public void TriggerOnCapture()
    {
        var copy = new List<Action<Vector3Int>>(onCaptureEffects);

        foreach (var effect in copy)
        {
            effect?.Invoke(Pos);
        }
    }

    /// <summary>
    /// 기물을 잡았을 때 발생하는 액션을 추가합니다.
    /// </summary>
    /// <param name="effect">발생할 효과</param>
    public void AddOnCaptureEffect(Action<Vector3Int> effect)
    {
        onCaptureEffects.Add(effect);
    }

    /// <summary>
    /// 기물을 잡았을 때 발생하는 액션을 제거합니다.
    /// </summary>
    /// <param name="effect">제거될 효과</param>
    public void RemoveOnCaptureEffect(Action<Vector3Int> effect)
    {
        onCaptureEffects.Remove(effect);
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

    /// <summary>
    /// 선택자가 효과 적용을 못하도록 제한
    /// </summary>
    public void NotSelect()
    {
        Debug.Log("이 기물은 선택할 수 없습니다!");
    }

    /// <summary>
    /// 현재 지정된 타입을 Char 형식으로 반환합니다.
    /// </summary>
    /// <returns>PieceType이 FEN 코드로 반환됩니다.</returns>
    public char TypeToChar()
    {
        return type switch
        {
            PieceType.Pawn   => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook   => 'r',
            PieceType.Queen  => 'q',
            PieceType.King   => 'k',
            _                => '?'
        };
    }

    public virtual string GetFen() { return ""; }
}
