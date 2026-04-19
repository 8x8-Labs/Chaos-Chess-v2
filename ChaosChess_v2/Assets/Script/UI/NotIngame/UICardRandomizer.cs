using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UICardRandomizer : MonoBehaviour
{
    [SerializeField] private GameObject[] cardPrefabs;

    /// <summary>
    /// 현재 보유한 카드를 제외하고 중복 없이 랜덤 카드들을 선택하여 반환합니다.
    /// </summary>
    public List<GameObject> GetRandomCards(List<GameObject> ownedCards, int count)
    {
        // 보유 카드 기준 필터링
        HashSet<GameObject> ownedSet = new HashSet<GameObject>(ownedCards);

        List<GameObject> availableCards = cardPrefabs
            .Where(card => !ownedSet.Contains(card))
            .ToList();

        // 셔플
        for (int i = availableCards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (availableCards[i], availableCards[j]) = (availableCards[j], availableCards[i]);
        }

        // 개수 제한
        int takeCount = Mathf.Min(count, availableCards.Count);
        return availableCards.Take(takeCount).ToList();
    }
}
