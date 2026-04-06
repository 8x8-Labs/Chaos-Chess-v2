using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정신 집중 - 기물 전용 (전설, 아마존)
/// 선택 기물은 일정 수치 동안 상호작용할 수 없고, 이후 아마존으로 승격됩니다.
/// </summary>
public class ConcentrationCard : CardData, IPieceCard
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
        // TODO: 선택된 기물을 DataSO.PieceLimitTurn 동안 상호작용 불가 상태로 설정
        //       이후 아마존(커스텀 기물)으로 승격 처리
    }
}
