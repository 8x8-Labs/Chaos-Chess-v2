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
        List<Piece> targets = BoardManager.Instance.GetAllPieces()
            .FindAll(piece => piece.Color != casterColor);

        targets.Sort((a, b) =>
        {
            return a.Color == PieceColor.White
                ? a.Pos.y.CompareTo(b.Pos.y)
                : b.Pos.y.CompareTo(a.Pos.y);
        });

        foreach (Piece piece in targets)
        {
            Vector3Int cur = piece.Pos;
            Vector3Int nx = new Vector3Int(cur.x, cur.y + (piece.Color == PieceColor.White ? -1 : 1), cur.z);
            if (BoardManager.Instance.IsInside(nx) && !IsOccupied(nx))
                BoardManager.Instance.ForceTeleport(piece, nx);
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
