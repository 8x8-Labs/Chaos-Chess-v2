using System.Collections.Generic;
using UnityEngine;

public class TestSkill : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    [ContextMenu("Start")]
    public void LoadPieceSelector()
    {
        if(selector == null) selector = FindFirstObjectByType<PieceSelector>();

        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Debug.Log($"카드 실행! : {DataSO.CardName}");
        List<Piece> pieces = args.Targets;
    }
}
