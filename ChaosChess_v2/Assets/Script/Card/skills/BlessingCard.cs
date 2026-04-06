using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가호 - 타일 전용 (고급)
/// 선택한 칸에 기물이 2턴 동안 있을 경우 다음 등급으로 승격됩니다.
/// 중간에 기물이 빠지면 타일 효과가 사라집니다.
/// </summary>
public class BlessingCard : CardData, ITileCard
{
    private TileSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        List<Vector3Int> tiles = args.TargetPos;
        // TODO: 선택된 칸에 가호 타일 효과 적용
        //       해당 칸에 기물이 DataSO.MaintainTurn(2)턴 연속으로 있으면 해당 기물을 다음 등급으로 승격
        //       기물이 칸을 벗어나면 효과 제거

        
    }
}
