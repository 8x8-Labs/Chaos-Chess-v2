using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 배수진 - 전역
/// 이 카드 사용 시 자신과 상대 모두 3턴 동안 기물을 전진시키는 방향으로만 이동할 수 있습니다.
/// </summary>
public class StagFightCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        var effector = CreateGlobalEffector<StagFightEffector>();
        effector.Apply();
        GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, effector.Revert);
    }
}
public class StagFightEffector : GlobalEffector
{
    List<Piece> changed = new();
    protected override void OnApply()
    {
        List<Piece> pieces = BoardManager.Instance.GetAllPieces();
        foreach (Piece piece in pieces)
        {
            if (piece.FenOverride != null || piece.MoveFenOverride != null)
                continue;
            changed.Add(piece);
            switch (piece.Type)
            {
                case PieceType.Rook:
                    piece.MoveFenOverride = "o";
                    break;
                case PieceType.Bishop:
                    piece.MoveFenOverride = "i";
                    break;
                case PieceType.Queen:
                    piece.MoveFenOverride = "h";
                    break;
                case PieceType.Knight:
                    piece.MoveFenOverride = "g";
                    break;
                case PieceType.King:
                    piece.MoveFenOverride = "j";
                    break;
                default:
                    changed.Remove(piece);
                    break;
            }

        }

        BoardManager.Instance.RefreshMoves();
        foreach (Piece piece in changed)
            Debug.Log(piece.Type);
    }

    protected override void OnRevert()
    {
        foreach (Piece piece in changed)
        {
            string p = piece.MoveFenOverride;
            if (p == "o" || p == "i" || p == "h" || p == "g" || p == "j")
            {
                piece.MoveFenOverride = null;
            }
        }
        BoardManager.Instance.RefreshMoves();
    }
}