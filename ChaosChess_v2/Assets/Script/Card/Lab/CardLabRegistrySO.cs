using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 이펙트 랩에서 테스트할 카드 프리팹 목록입니다.
/// 각 항목은 프리팹 루트의 <see cref="CardData"/> 컴포넌트 참조입니다.
/// 목록은 에디터의 "프리팹 스캔" 버튼(CardLabRegistryEditor)으로 자동 채울 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "CardLabRegistry", menuName = "Card/Card Lab Registry")]
public class CardLabRegistrySO : ScriptableObject
{
    [Tooltip("프리팹 루트에 CardData가 부착된 카드 프리팹 목록입니다.")]
    public List<CardData> Cards = new List<CardData>();

    public int Count => Cards != null ? Cards.Count : 0;

    /// <summary>드롭다운에 표시할 라벨을 생성합니다. (카드명 [타입])</summary>
    public string GetDisplayName(int index)
    {
        if (Cards == null || index < 0 || index >= Cards.Count || Cards[index] == null)
            return "<null>";

        CardData card = Cards[index];
        CardDataSO so = card.DataSO;

        if (so == null)
            return $"{card.name} <DataSO 없음>";

        string cardName = string.IsNullOrEmpty(so.CardName) ? card.name : so.CardName;
        return $"{cardName} [{so.Type}]";
    }
}
