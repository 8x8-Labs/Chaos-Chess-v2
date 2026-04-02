using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어둠 속의 손 - 기물 전용 (희귀)
/// 나이트, 룩, 비숍, 폰에 적용 가능. 2턴간 움직일 수 없도록 고정시킵니다.
/// </summary>
public class DarkHandCard : CardData, IPieceCard
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
        // TODO: 선택된 기물을 2턴간 이동 불가(고정) 상태로 처리
    }
}
