using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투기장 - 타일 전용 (투기장)
/// 지정된 구역에 랜덤한 기물이 배치되며, 해당 구역에서 기물들이 전투를 벌입니다.
/// </summary>
public class ArenaCard : CardData, ITileCard
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
        // TODO: 선택된 칸에 투기장 효과 적용
        //       해당 구역에 랜덤한 기물 배치 및 투기장 카메라 활성화 처리
    }
}
