using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 위압 - 전역
/// 상대방의 모든 기물이 1칸 후퇴합니다.
/// </summary>
public class OverbearingCard : CardData, ICard
{

    public void Execute(CardEffectArgs args = null)
    {
        var effector = CreateGlobalEffector<OverbearingEffector>();
        PieceColor casterColor = args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor();
        effector.SetCasterColor(casterColor);
        effector.SetSuppressAutomaticTurnEnd(args != null && args.SuppressAutomaticTurnEnd);
        effector.Apply();
    }
}
public class OverbearingEffector : GlobalEffector
{
    private PieceColor casterColor = PieceColor.White;
    private bool suppressAutomaticTurnEnd;

    public void SetCasterColor(PieceColor color)
    {
        casterColor = color;
    }

    public void SetSuppressAutomaticTurnEnd(bool value)
    {
        suppressAutomaticTurnEnd = value;
    }

    protected override void OnApply()
    {
        List<Piece> pieces = BoardManager.Instance.GetAllPieces();
        foreach (Piece piece in pieces)
        {
            Debug.Log(piece.Type);
            if (piece.Color == casterColor)
                continue;
            Vector3Int cur = piece.Pos;
            Vector3Int nx = new Vector3Int(cur.x, cur.y + (piece.Color == PieceColor.White ? -1 : 1), cur.z);
            if (BoardManager.Instance.IsInside(nx) && !IsOccupied(nx))
                BoardManager.Instance.ForceTeleport(piece, nx);
            else
            {
                //Debug.Log(BoardManager.Instance.IsInside(nx));
                Debug.Log(IsOccupied(nx));
            }
        }
        BoardManager.Instance.RefreshMoves();
        if (!suppressAutomaticTurnEnd)
            GameManager.Instance.NextTurn(() => GameManager.Instance.RequestAIMove());

        Revert();
    }

    protected override void OnRevert()
    {
        Destroy(this);
    }
    private bool IsOccupied(Vector3Int candidate)
    {
        Piece p = BoardManager.Instance.GetPiece(candidate);
        if (p == null)
            return false;
        return true;
    }
}
