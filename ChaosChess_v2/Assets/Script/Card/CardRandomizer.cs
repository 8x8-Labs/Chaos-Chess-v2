using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardRandomizer : MonoBehaviour
{
    [SerializeField] private Transform content;

    private int currentCardCnt = 0;
    public int CurrentCardCnt => currentCardCnt;

    private Dictionary<GameObject, GameObject> _activeCards = new();

    private CardRandomizerManager cardRandomizerManager;

    private void Awake()
    {
        cardRandomizerManager = CardRandomizerManager.Instance;
    }

    /// <summary>
    /// 지정된 카드 풀에서 현재 활성 카드와
    /// 중복되지 않는 랜덤 카드를 생성합니다.
    /// </summary>
    public void GenerateCard(List<GameObject> pool, int count = 1)
    {
        List<GameObject> usedCards =
            _activeCards.Values.ToList();

        List<GameObject> randomCards =
            cardRandomizerManager.GetRandomCardsFromPool(
                pool,
                usedCards,
                count
            );

        if (randomCards.Count == 0)
            return;

        foreach (GameObject cardPrefab in randomCards)
        {
            currentCardCnt++;

            GameObject instance =
                Instantiate(cardPrefab, content);

            _activeCards[instance] = cardPrefab;
        }
    }
    public void RemoveCard(GameObject card)
    {
        card.GetComponent<CardAnim>().DestroyCard();

        currentCardCnt--;

        _activeCards.Remove(card);
    }
}