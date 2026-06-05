using System.Collections.Generic;
using System.Linq;
using Action = System.Action;
using UnityEngine;

public class CardRandomizerManager : MonoBehaviour
{
    public static CardRandomizerManager Instance;

    [SerializeField] private List<GameObject> allCards;
    public IReadOnlyList<GameObject> AllCards => allCards;
    private readonly Dictionary<CardDataSO, int> activeCardCounts = new();
    private CardDataSO cardExecutionSource;

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

    public void ClearActiveCards()
    {
        activeCardCounts.Clear();
        cardExecutionSource = null;
    }

    public void ExecuteCard(CardDataSO cardSO, Action action)
    {
        if (action == null)
            return;

        if (cardSO == null)
        {
            action.Invoke();
            return;
        }

        CardDataSO previousExecutionSource = cardExecutionSource;
        cardExecutionSource = cardSO;
        try
        {
            if (cardSO != null)
            {
                SoundManager.Instance?.PlayCardUseSFX(cardSO.CardTier);
            }
            action.Invoke();
        }
        finally
        {
            cardExecutionSource = previousExecutionSource;
        }
    }

    public ActiveCardToken RetainActiveCard(CardDataSO cardSO = null)
    {
        cardSO ??= cardExecutionSource;
        if (cardSO == null) return null;

        activeCardCounts.TryGetValue(cardSO, out int count);
        activeCardCounts[cardSO] = count + 1;

        return new ActiveCardToken(this, cardSO);
    }

    private bool IsCardActive(CardDataSO cardSO)
    {
        return cardSO != null && activeCardCounts.ContainsKey(cardSO);
    }

    private void DecrementCount(CardDataSO cardSO)
    {
        if (cardSO == null || !activeCardCounts.TryGetValue(cardSO, out int count)) return;

        if (count <= 1)
            activeCardCounts.Remove(cardSO);
        else
            activeCardCounts[cardSO] = count - 1;
    }

    /// <summary>
    /// 특정 풀 내부에서 특정 카드를 제외한 랜덤 카드 선택
    /// </summary>
    public List<GameObject> GetRandomCardsFromPool(List<GameObject> pool, List<GameObject> excludedCards, int count)
    {
        List<GameObject> availableCards = pool
            .Except(excludedCards)
            .Where(card => !IsCardActive(GetCardSO(card)))
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
            .Where(card => !IsCardActive(GetCardSO(card)))
            .ToList();

        Shuffle(availableCards);

        return availableCards
            .Take(Mathf.Min(count, availableCards.Count))
            .ToList();
    }

    public List<GameObject> GetRandomCardsByTier(Tier tier, int count)
    {
        List<GameObject> availableCards = allCards
            .Where(card =>
            {
                CardData data = card.GetComponent<CardData>();

                return data != null &&
                       data.DataSO != null &&
                       data.DataSO.CardTier == tier &&
                       !IsCardActive(data.DataSO);
            })
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

    private CardDataSO GetCardSO(GameObject card)
    {
        if (card == null) return null;

        CardData data = card.GetComponent<CardData>();
        return data != null ? data.DataSO : null;
    }

    public sealed class ActiveCardToken
    {
        private readonly CardRandomizerManager owner;
        private readonly CardDataSO cardSO;
        private bool isActive = true;

        public ActiveCardToken(CardRandomizerManager owner, CardDataSO cardSO)
        {
            this.owner = owner;
            this.cardSO = cardSO;
        }

        public void Complete()
        {
            if (!isActive) return;
            isActive = false;
            owner.DecrementCount(cardSO);
        }
    }
}
