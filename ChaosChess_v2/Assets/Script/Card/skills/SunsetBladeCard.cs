using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노을빛 검 - 기물 전용 (희귀)
/// 폰에 적용. 상대 기물을 잡을 때 기물 옆에 있는 2개의 기물도 함께 잡힙니다 (1회 한정).
/// </summary>
public class SunsetBladeCard : CardData, IPieceCard
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
        // TODO: 선택된 폰에게 다음 기물 포획 시 인접한 기물 2개 추가 포획 효과 부여 (1회 한정)
    }
}
