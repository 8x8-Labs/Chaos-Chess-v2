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
    public event System.Action<int> OnCardCountChanged;

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
    public int GenerateCard(List<GameObject> pool, int count = 1)
    {
        // 원격 대전이면 합의된 큐에서 순서대로 꺼냅니다.
        // 양쪽이 각자 뽑으면 손패가 달라지고, 상대가 쓴 카드를 이쪽에서 찾지 못하게 됩니다.
        List<GameObject> randomCards = TryTakeFromAgreedQueue(count)
            ?? cardRandomizerManager.GetRandomCardsFromPool(
                pool,
                _activeCards.Values,
                count
            );

        if (randomCards.Count == 0)
            return 0;

        currentCardCnt += randomCards.Count;
        OnCardCountChanged?.Invoke(currentCardCnt);

        // 코루틴으로 카드 딜레이 스폰 기능 부여
        StartCoroutine(SpawnCard(randomCards, spawnDelay));
        return randomCards.Count;
    }

    // 합의된 카드 큐에서 지금까지 꺼낸 개수입니다.
    private int agreedQueueCursor;

    /// <summary>
    /// 합의된 카드 큐에서 다음 count장을 꺼냅니다.
    /// 큐가 없으면(단일 플레이) null을 돌려주어 호출측이 기존 방식으로 뽑게 합니다.
    /// </summary>
    private List<GameObject> TryTakeFromAgreedQueue(int count)
    {
        string[] queue = GameCycleManager.Instance?.MatchSetup?.CardQueue;
        if (queue == null || queue.Length == 0)
            return null;

        List<GameObject> taken = new List<GameObject>();

        while (taken.Count < count && agreedQueueCursor < queue.Length)
        {
            string cardId = queue[agreedQueueCursor++];

            GameObject prefab = FindCardPrefabById(cardId);
            if (prefab == null)
            {
                Debug.LogError($"[Match] 합의된 카드 '{cardId}'를 찾지 못했습니다. 카드 DB가 다를 수 있습니다.");
                continue;
            }

            taken.Add(prefab);
        }

        if (taken.Count < count)
            Debug.LogWarning($"[Match] 카드 큐가 바닥났습니다. 요청 {count}장 중 {taken.Count}장만 꺼냈습니다.");

        return taken;
    }

    /// <summary>AiCardId로 카드 프리팹을 찾습니다.</summary>
    private GameObject FindCardPrefabById(string cardId)
    {
        if (cardRandomizerManager == null || cardRandomizerManager.AllCards == null)
            return null;

        foreach (GameObject cardObject in cardRandomizerManager.AllCards)
        {
            if (cardObject == null) continue;

            CardData cardData = cardObject.GetComponent<CardData>();
            if (cardData == null || cardData.DataSO == null) continue;

            if (cardData.DataSO.AiCardId == cardId)
                return cardObject;
        }

        return null;
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
        if (card == null) return;

        // 실제로 활성 목록에 있던 카드를 제거했을 때만 카운트를 줄여 음수·desync를 방지한다.
        if (!_activeCards.Remove(card)) return;

        card.GetComponent<CardAnim>()?.DestroyCard();

        currentCardCnt--;
        OnCardCountChanged?.Invoke(currentCardCnt);
    }
}
