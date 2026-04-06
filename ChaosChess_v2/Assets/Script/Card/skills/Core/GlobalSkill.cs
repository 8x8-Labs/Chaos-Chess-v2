using System.Collections.Generic;
using UnityEngine;

public class GlobalSkill : CardData, ICard
{
    [ContextMenu("Global Execute")]
    public void ExecuteTest()
    {
        Execute();
    }

    public void Execute(CardEffectArgs args = null)
    {
        Debug.Log($"카드 실행! : {DataSO.CardName}");
        Debug.Log($"제한 턴 여부: {DataSO.HasLimit}");
    }
}
