using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 가스라이팅 - 기물 전용 (희귀)
/// 나이트, 비숍, 폰에 적용됩니다.
/// 랜덤한 상대 기물(킹, 퀸, 룩 제외)을 자신의 기물로 변환합니다.
/// </summary>
public class GaslightingCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        Piece p = GetRandomPiece();
        if (p == null) return;

        BoardManager.Instance.ChangePiece(
            pos: p.Pos,
            color: GameManager.Instance.PlayerColor,
            type: p.TypeToChar());
    }

    private Piece GetRandomPiece()
    {
        List<Piece> pieces = BoardManager.Instance.GetAllPieces()
            .Where(p => p.Color == GameManager.Instance.EnemyColor &&
            (DataSO.PieceType & p.Type) != 0).ToList();

        int rand = Random.Range(0, pieces.Count);
        Debug.Log(rand);
        return pieces[rand];
    }
}
