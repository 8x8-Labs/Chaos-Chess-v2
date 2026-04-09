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

    /// <summary>1회 죽음 무효화 플래그. AI가 이 기물을 잡으려 하면 취소됩니다.</summary>
    public bool IsInvincible { get; private set; } = false;

    public void SetInvincible()
    {
        IsInvincible = true;
    }

    public void ConsumeInvincibility()
    {
        IsInvincible = false;
    }

    [SerializeField] protected Sprite WhitePiece;
    [SerializeField] protected Sprite BlackPiece;
    [SerializeField] protected PieceType type;
    protected SpriteRenderer spriteRenderer;

    [SerializeField] private PieceColor color;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Vector3Int pos;
    private string _fenOverride;

    private string _moveFenOverride; // 행마 전용 (t, u, p2 등)

    protected List<Vector3Int> canMovePos;

    private MaterialPropertyBlock mpb;
    private static readonly int OutlineThickId = Shader.PropertyToID("_OutlineThick");

    public List<Vector3Int> CanMovePos
    {
        get
        {
            return canMovePos;
        }
    }
    public PieceColor Color
    {
        get { return color; }
        set { color = value; }
    }

    public PieceValue Value
    {
        get
        {
            return type switch
            {
                PieceType.Pawn => PieceValue.Pawn,
                PieceType.Knight => PieceValue.Knight,
                PieceType.Bishop => PieceValue.Bishop,
                PieceType.Rook => PieceValue.Rook,
                PieceType.Queen => PieceValue.Queen,
                PieceType.King => PieceValue.King,
                PieceType.Amazon => PieceValue.Amazon,
                PieceType.Chancellor => PieceValue.Chancellor,
                PieceType.KnightRider => PieceValue.KnightRider,
                PieceType.Wall => PieceValue.Wall,
                _ => 0
            };
        }
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
    /// 효과를 통해 행마만 변경된 기물의 FEN을 저장합니다.
    /// </summary>
    public string MoveFenOverride
    {
        get
        {
            if (_moveFenOverride == null) return null;

            return Color == PieceColor.White
                ? _moveFenOverride.ToUpper()
                : _moveFenOverride.ToLower();
        }
        set
        {
            if (value == null)
            {
                _moveFenOverride = null;
            }
            else
            {
                string v =
                    Color == PieceColor.White
                    ? value.ToUpper()
                    : value.ToLower();
                _moveFenOverride = v;
            }
        }
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
            if (value == null)
            {
                _fenOverride = null;
                ResetSprite();
            }
            else
            {
                string v =
                    Color == PieceColor.White
                    ? value.ToUpper()
                    : value.ToLower();
                _fenOverride = v;
                UpdateSprite();
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
        foreach (Vector3Int pos in canMovePos)
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
        canMovePos = new List<Vector3Int>();
    }

    public void AddCanMovePos(Vector3Int pos)
    {
        canMovePos.Add(pos);
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

        GetComponent<IPieceEffect>()?.OnPieceCapture();

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
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            PieceType.Amazon => 's',
            PieceType.Chancellor => 'y',
            PieceType.KnightRider => 'z',
            PieceType.Wall => 'a',
            _ => '?'
        };
    }

    /// <summary>
    /// FEN 코드 Char를 PieceType으로 반환합니다.
    /// </summary>
    /// <param name="c">FEN 코드 문자</param>
    /// <returns>해당하는 PieceType</returns>
    //public PieceType CharToType(char c)
    //{
    //    return char.ToLower(c) switch
    //    {
    //        'p' => PieceType.Pawn,
    //        'n' => PieceType.Knight,
    //        'b' => PieceType.Bishop,
    //        'r' => PieceType.Rook,
    //        'q' => PieceType.Queen,
    //        'k' => PieceType.King,
    //        's' => PieceType.Amazon,
    //        'y' => PieceType.Chancellor,
    //        'z' => PieceType.KnightRider,
    //        'a' => PieceType.Wall,
    //        _ => throw new ArgumentException($"알 수 없는 FEN 문자: {c}")
    //    };
    //}

    public int CharToIndex(char c)
    {
        return char.ToLower(c) switch
        {
            'p' => 0,  // Pawn
            'b' => 1,  // Bishop
            'r' => 2,  // Rook
            'n' => 3,  // Knight
            'z' => 4,  // KnightRider
            'y' => 5,  // Chancellor
            's' => 6,  // Amazon
            'q' => 7,  // Queen
            'k' => 8,  // King
            'a' => 9,  // Wall
            'o' => 10,
            'i' => 11,
            'h' => 12,
            'g' => 13,
            'j' => 14,
            _ => throw new ArgumentException($"알 수 없는 FEN 문자: {c}")
        };
    }

    /// <summary>
    /// 현재 오버라이드된 FEN에 맞춰 기물의 스프라이트를 관리합니다.
    /// </summary>
    private void UpdateSprite()
    {
        GameManager gm = GameManager.Instance;

        PieceColor color = Color;
        int index = CharToIndex(_fenOverride[0]);

        Sprite sprite = color == PieceColor.White ?
            gm.WhiteSprites[index] :
            gm.BlackSprites[index];

        spriteRenderer.sprite = sprite;
    }
    /// <summary>
    /// 오버라이드된 스프라이트를 초기화합니다.
    /// </summary>
    private void ResetSprite()
    {
        if (Color == PieceColor.White)
            spriteRenderer.sprite = WhitePiece;
        else
            spriteRenderer.sprite = BlackPiece;
    }

    public virtual string GetFen() { return ""; }
}
