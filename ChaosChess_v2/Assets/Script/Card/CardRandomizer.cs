using System.Collections.Generic;
using UnityEngine;

public class CardRandomizer : MonoBehaviour
{
    [SerializeField] private GameObject[] cardPrefabs;
    [SerializeField] private Transform content;
    [SerializeField] private int startCard = 3;

    private int currentCardCnt = 0;
    private Dictionary<GameObject, GameObject> _activeCards = new();

    private void Start()
    {
        for (int i = 0; i < startCard; i++) GenerateCards();
    }

    /// <summary>내장 cardPrefabs 풀에서 랜덤 카드를 생성합니다.</summary>
    public void GenerateCards() => GenerateCard(new List<GameObject>(cardPrefabs));

    /// <summary>지정된 풀에서 중복 없이 랜덤 카드를 하나 생성합니다.</summary>
    public void GenerateCard(List<GameObject> pool)
    {
        HashSet<GameObject> usedPrefabs = new HashSet<GameObject>(_activeCards.Values);

        List<GameObject> available = new List<GameObject>();
        foreach (var prefab in pool)
        {
            if (!usedPrefabs.Contains(prefab))
                available.Add(prefab);
        }

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
