using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무한궤도 - 기물 전용 (고급, 나이트라이더)
/// 나이트가 2턴 동안 나이트라이더의 행마법대로 이동할 수 있습니다.
/// </summary>
public class InfiniteTrackCard : CardData, IPieceCard
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
        // TODO: 선택된 나이트에게 DataSO.PieceLimitTurn(2)턴 동안 나이트라이더(커스텀 기물) 행마법 적용
    }
}
