using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardRandomizer : MonoBehaviour
{
    [SerializeField] private Transform content;
    [SerializeField] private float spawnDelay = 0.2f;

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

        currentCardCnt += randomCards.Count;

        // 코루틴으로 카드 딜레이 스폰 기능 부여
        StartCoroutine(SpawnCard(randomCards, spawnDelay));
    }

    private IEnumerator SpawnCard(List<GameObject> list, float delay)
    {
        foreach (GameObject cardPrefab in list)
        {
            GameObject instance =
                Instantiate(cardPrefab, content);

            _activeCards[instance] = cardPrefab;

            yield return new WaitForSeconds(delay);
        }
    }

    public void RemoveCard(GameObject card)
    {
        card.GetComponent<CardAnim>().DestroyCard();

        currentCardCnt--;

        _activeCards.Remove(card);
    }
}