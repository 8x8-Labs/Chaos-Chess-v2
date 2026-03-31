using System.Collections.Generic;
using UnityEngine;

public class TestSkill : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Debug.Log("카드 실행!");
        List<Piece> pieces = args.Targets;
    }
}
