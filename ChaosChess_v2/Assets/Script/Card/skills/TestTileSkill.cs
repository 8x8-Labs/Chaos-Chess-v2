using System.Collections.Generic;
using UnityEngine;

public class TestTileSkill : CardData, ITileCard
{
    private TileSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    public void Execute(CardEffectArgs args = null)
    {
        Debug.Log($"카드 실행! : {DataSO.name}");
        List<Vector3Int> pieces = args.TargetPos;
    }

    [ContextMenu("Test")]
    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();

        selector.EnableSelector(this);
    }
}
