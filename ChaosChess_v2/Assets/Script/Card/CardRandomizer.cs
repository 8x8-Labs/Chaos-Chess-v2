using System.Collections.Generic;
using UnityEngine;

public class CardRandomizer : MonoBehaviour
{
    [SerializeField] private GameObject[] cardPrefabs;
    [SerializeField] private Transform content;
    [SerializeField] private int drawCount = 3;
    [SerializeField] private int startCard = 3;

    private int currentCardCnt = 0;

    private void Start()
    {
        for (int i = 0; i < startCard; i++) GenerateCards();
    }

    public void GenerateCards()
    {
        //foreach (Transform child in content)
        //    Destroy(child.gameObject);

        List<GameObject> pool = new List<GameObject>(cardPrefabs);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int count = Mathf.Min(drawCount, pool.Count);
        for (int i = 0; i < count; i++)
        {
            currentCardCnt++;
            Instantiate(pool[i], content);
        }
    }

    public void RemoveCard(GameObject card)
    {
        card.GetComponent<CardAnim>().DestroyCard();
        currentCardCnt--;
    }
}
