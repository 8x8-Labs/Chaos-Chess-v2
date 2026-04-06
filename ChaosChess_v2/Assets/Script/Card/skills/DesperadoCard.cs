using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데스페라도 - 기물 전용 (고급)
/// 아군 기물을 선택하여 시전자가 한 번 더 행동하게 하고, 선택한 기물은 죽게 됩니다.
/// </summary>
public class DesperadoCard : CardData, IPieceCard
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
        // TODO: 선택된 아군 기물을 제거하고 현재 플레이어에게 추가 행동권 1회 부여 처리
    }
}
