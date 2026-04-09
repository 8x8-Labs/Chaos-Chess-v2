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
        // CreateGlobalEffector는 LimitTurn으로 초기화하지만 Revert는 AppendAction(PieceLimitTurn)으로만 처리.
        // 내부 카운트다운이 먼저 만료되어 조기 초기화되는 문제를 막기 위해 영구 지속으로 덮어씀.
        effector.Init(DataSO.PieceType, ApplyType.All, DataSO.LimitTurn);
        effector.Apply();
        // GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, effector.Revert);
    }
}
public class StagFightEffector : GlobalEffector
{
    private readonly List<Piece> changed = new();
    private static readonly Dictionary<PieceType, string> MoveOverrides = new()
    {
        { PieceType.Rook, "o" },
        { PieceType.Bishop, "i" },
        { PieceType.Queen, "h" },
        { PieceType.Knight, "g" }
    };
    private static readonly HashSet<string> RevertableFens = new(MoveOverrides.Values);

    protected override void OnApply()
    {
        List<Piece> pieces = BoardManager.Instance.GetAllPieces();
        foreach (Piece piece in pieces)
        {
            if (piece.FenOverride != null || piece.MoveFenOverride != null)
                continue;

            if (MoveOverrides.TryGetValue(piece.Type, out var overrideFen))
            {
                piece.MoveFenOverride = overrideFen;
                changed.Add(piece);
            }
        }

        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        foreach (Piece piece in changed)
        {
            string p = piece.MoveFenOverride?.ToLower();
            if (p != null && RevertableFens.Contains(p))
                piece.MoveFenOverride = null;
        }
        BoardManager.Instance.RefreshMoves();
        Destroy(gameObject);
    }
}