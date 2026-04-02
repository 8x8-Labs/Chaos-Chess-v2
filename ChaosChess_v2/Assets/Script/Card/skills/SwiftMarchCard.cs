using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 신속행군 - 기물 전용 (일반)
/// 폰 기물이 전방으로 2칸 전진할 수 있습니다.
/// </summary>
public class SwiftMarchCard : CardData, IPieceCard
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
        // TODO: 선택된 폰이 이번 턴 전방 2칸 전진 가능하도록 처리
    }
}
