using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가스라이팅 - 기물 전용 (희귀)
/// 나이트, 비숍, 폰에 적용됩니다.
/// 랜덤한 상대 기물(킹, 퀸, 룩 제외)을 자신의 기물로 변환합니다.
/// </summary>
public class GaslightingCard : CardData, IPieceCard
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
        // TODO: 선택된 아군 기물(나이트/비숍/폰) 기준으로
        //       상대 기물 중 킹/퀸/룩 제외한 랜덤 기물 1개를 아군 기물로 변환 처리
    }
}
