using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 거대화 - 기물 전용 (무력화)
/// 기물 이동 시 1칸 반경의 기물들을 1턴 동안 무력화시킵니다.
/// 무력화 시 카드 적용은 되지만 이동할 수는 없습니다.
/// </summary>
public class GiantCard : CardData, IPieceCard
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
        // TODO: 선택된 기물에게 거대화 효과 부여
        //       해당 기물이 이동할 때마다 1칸 반경의 기물들을 1턴 동안 이동 불가(무력화) 처리
    }
}
