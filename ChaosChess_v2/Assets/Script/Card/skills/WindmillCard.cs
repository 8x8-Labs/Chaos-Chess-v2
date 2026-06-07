using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 풍차 - 기물 전용 (고급)
/// 2턴 동안 룩은 비숍의 행마법대로, 비숍은 룩의 행마법대로 움직입니다.
/// </summary>
public class WindmillCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        var effector = CreateGlobalEffector<WindmillEffector>();
        ApplyType casterColor = GameManager.Instance.turnColor == PieceColor.White
            ? ApplyType.White
            : ApplyType.Black;
        effector.Init(DataSO.PieceType, casterColor, DataSO.PieceLimitTurn);
        effector.Apply();

    }
}
public class WindmillEffector : GlobalEffector
{
    private readonly List<Piece> changed = new();

    protected override void OnApply()
    {
        foreach (Piece piece in BoardManager.Instance.GetAllPieces())
        {
            if (!IsWatching(piece))
                continue;

            if (PieceEffector.HasActiveMovementOverride(piece))
                continue;

            string overrideFen = piece.Type == PieceType.Rook ? "b" : piece.Type == PieceType.Bishop ? "r" : null;
            if (overrideFen == null)
                continue;

            piece.MoveFenOverride = overrideFen;
            changed.Add(piece);
        }

        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        foreach (Piece piece in changed)
        {
            string p = piece != null ? piece.MoveFenOverride?.ToLower() : null;
            if (p == "b" || p == "r")
                piece.MoveFenOverride = null;
        }
        changed.Clear();
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}
