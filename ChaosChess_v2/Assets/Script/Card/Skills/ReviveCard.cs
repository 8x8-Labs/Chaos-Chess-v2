using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// ��Ȱ - Ÿ�� ���� (����)
/// ������ Ÿ�Ͽ��� ���� �⹰ �� ���� ��ġ�� ���� �⹰�� ��Ȱ�մϴ�
/// </summary>
public class ReviveCard : CardData, ITileCard
{
    private TileSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();
        selector.EnableSelector(this);
    }
    public void Execute(CardEffectArgs args = null)
    {
        ReviveEffector effector = CreateTileEffector<ReviveEffector>(args.TargetPos[0]);
        effector.Apply();
    }
}

public class ReviveEffector : TileEffector
{
    private PieceValue GetValue(PieceType type)
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
    private char TypeToChar(PieceType type)
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
    protected override void OnApply()
    {
        PieceColor tc = GameManager.Instance.turnColor;
        List<PieceType> pieces = null;
        if (tc == PieceColor.White)
            pieces = BoardManager.Instance.WhiteDeadPieces;
        else
            pieces = BoardManager.Instance.BlackDeadPieces;
        int maxv = 0;
        PieceType res = PieceType.Wall;
        if (pieces != null)
        {
            foreach (PieceType piece in pieces)
            {
                int g = (int)GetValue(piece);
                if (g > maxv)
                {
                    maxv = g;
                    res = piece;
                }
            }
        }
        pieces.Remove(res);
        BoardManager.Instance.ChangePiece(TilePos, GameManager.Instance.turnColor, TypeToChar(res));
        Revert();
        GameManager.Instance.NextTurn(() => GameManager.Instance.RequestAIMove());
    }

    protected override void OnRevert()
    {
        Destroy(gameObject);
    }
}