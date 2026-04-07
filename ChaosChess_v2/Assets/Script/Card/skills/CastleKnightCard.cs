using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성 위의 말 - 기물 전용 (챈슬러 합체)
/// 턴 소모 없이 나이트, 룩 기물을 하나씩 선택함.
/// 나이트가 룩 자리로 이동해 합쳐져 챈슬러로 승격 (나이트가 있던 자리에는 기물이 남지 않음).
/// </summary>
public class CastleKnightCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        List<Piece> pieces = args.Targets;
        // pieces[0]: 나이트, pieces[1]: 룩
        // TODO: 나이트를 룩 위치로 이동, 두 기물을 챈슬러(커스텀 기물)로 합체 승격
        //       나이트 원래 자리는 빈 칸으로 처리, 턴 소모 없음

        BoardManager.Instance.DestroyPiece(pieces[0]);
        BoardManager.Instance.ChangePiece(pieces[1].Pos, pieces[1].Color, 's');
    }
}
