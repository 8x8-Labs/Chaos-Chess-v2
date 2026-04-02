using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조준 - 기물 전용 (일반)
/// 폰 기물이 전방 사선으로 1회 이동할 수 있습니다.
/// </summary>
public class AimCard : CardData, IPieceCard
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
        // TODO: 선택된 폰에게 이번 턴 전방 사선 이동 1회 허용 처리
    }
}
