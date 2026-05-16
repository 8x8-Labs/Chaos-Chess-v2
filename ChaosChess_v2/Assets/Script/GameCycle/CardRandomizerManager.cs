using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardRandomizerManager : MonoBehaviour
{
    public static CardRandomizerManager Instance;

    [SerializeField] private List<GameObject> allCards;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 특정 풀 내부에서 특정 카드를 제외한 랜덤 카드 선택
    /// </summary>
    public List<GameObject> GetRandomCardsFromPool(List<GameObject> pool, List<GameObject> excludedCards, int count)
    {
        List<GameObject> availableCards = pool
            .Except(excludedCards)
            .ToList();

        Shuffle(availableCards);

        return availableCards
            .Take(Mathf.Min(count, availableCards.Count))
            .ToList();
    }

    /// <summary>
    /// 전체 카드 중 특정 카드 제외 후 랜덤 선택
    /// </summary>
    public List<GameObject> GetRandomCardsFromAll(List<GameObject> excludedCards, int count)
    {
        List<GameObject> availableCards = allCards
            .Except(excludedCards)
            .ToList();

        Shuffle(availableCards);

        return availableCards
            .Take(Mathf.Min(count, availableCards.Count))
            .ToList();
    }

    private void Shuffle(List<GameObject> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            (list[i], list[j]) =
                (list[j], list[i]);
        }
    }
}