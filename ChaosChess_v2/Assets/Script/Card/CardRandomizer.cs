using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardRandomizer : MonoBehaviour
{
    [SerializeField] private Transform content;

    private int currentCardCnt = 0;
    public int CurrentCardCnt => currentCardCnt;

    private Dictionary<GameObject, GameObject> _activeCards = new();

    /// <summary>지정된 풀에서 중복 없이 랜덤 카드를 하나 생성합니다.</summary>
    public void GenerateCard(List<GameObject> pool)
    {
        HashSet<GameObject> usedPrefabs = new HashSet<GameObject>(_activeCards.Values);

        List<GameObject> available = pool.Except(usedPrefabs).ToList();

        for (int i = available.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (available[i], available[j]) = (available[j], available[i]);
        }

        if (available.Count == 0) return;
        currentCardCnt++;
        var instance = Instantiate(available[0], content);
        _activeCards[instance] = available[0];
    }


    public void RemoveCard(GameObject card)
    {
        card.GetComponent<CardAnim>().DestroyCard();
        currentCardCnt--;
        _activeCards.Remove(card);
    }
}
