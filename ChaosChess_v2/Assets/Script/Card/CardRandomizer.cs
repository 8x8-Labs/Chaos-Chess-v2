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

    public void GenerateCards()
    {
        //foreach (Transform child in content)
        //    Destroy(child.gameObject);

        HashSet<GameObject> usedPrefabs = new HashSet<GameObject>(_activeCards.Values);

        List<GameObject> pool = new List<GameObject>();
        foreach (var prefab in cardPrefabs)
        {
            if (!usedPrefabs.Contains(prefab))
                pool.Add(prefab);
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        if (pool.Count == 0) return;
        currentCardCnt++;
        var instance = Instantiate(pool[0], content);
        _activeCards[instance] = pool[0];
    }

    public void RemoveCard(GameObject card)
    {
        card.GetComponent<CardAnim>().DestroyCard();
        currentCardCnt--;
        _activeCards.Remove(card);
    }
}
