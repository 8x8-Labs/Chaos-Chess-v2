using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public sealed class AiCardHand : MonoBehaviour
    {
        public const int MaxCards = 4;

        [SerializeField] private List<GameObject> startingCards = new();
        [SerializeField] private int defaultRemainingUses = 1;
        [SerializeField] private bool useEditorConfiguredStartingCards;
        [SerializeField] private List<string> editorStartingCardPrefabPaths = new();

        private readonly List<GameObject> runtimeCards = new();
        private bool runtimeCardsInitialized;
        private int version;

        public IReadOnlyList<GameObject> AvailableCards => CurrentCards;
        public int DefaultRemainingUses => Mathf.Max(0, defaultRemainingUses);
        public int Version => version;
        public static event Action<AiCardHand> AnyChanged;
        public event Action Changed;

        private void Awake()
        {
            TrimStartingCards();
            RebuildRuntimeCardsFromConfiguration();
        }

        private IReadOnlyList<GameObject> CurrentCards
        {
            get
            {
                if (Application.isPlaying && runtimeCardsInitialized)
                    return runtimeCards;

                return startingCards;
            }
        }

#if UNITY_EDITOR
        private List<GameObject> ResolveEditorConfiguredCards()
        {
            var resolvedCards = new List<GameObject>();

            if (!useEditorConfiguredStartingCards)
                return resolvedCards;

            foreach (string path in editorStartingCardPrefabPaths)
            {
                if (resolvedCards.Count >= MaxCards)
                    break;

                if (string.IsNullOrWhiteSpace(path))
                    continue;

                GameObject cardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (cardPrefab != null)
                {
                    AddUniqueCard(resolvedCards, cardPrefab);
                    continue;
                }

                Debug.LogWarning($"[AI Card Hand] Could not load editor card prefab at '{path}'.");
            }

            return resolvedCards;
        }
#endif

        public void RebuildRuntimeCardsFromConfiguration()
        {
            runtimeCards.Clear();

#if UNITY_EDITOR
            List<GameObject> configuredCards = ResolveEditorConfiguredCards();
            IReadOnlyList<GameObject> source = configuredCards.Count > 0 ? configuredCards : startingCards;
#else
            IReadOnlyList<GameObject> source = startingCards;
#endif

            foreach (GameObject card in source)
            {
                if (runtimeCards.Count >= MaxCards)
                    break;

                AddUniqueCard(runtimeCards, card);
            }

            runtimeCardsInitialized = true;
            NotifyChanged();
        }

        public void ReplaceRuntimeCards(IEnumerable<GameObject> cards)
        {
            runtimeCards.Clear();

            if (cards != null)
            {
                foreach (GameObject card in cards)
                {
                    if (runtimeCards.Count >= MaxCards)
                        break;

                    AddUniqueCard(runtimeCards, card);
                }
            }

            runtimeCardsInitialized = true;
            NotifyChanged();
        }

        private void OnValidate()
        {
            defaultRemainingUses = Mathf.Max(0, defaultRemainingUses);
            TrimStartingCards();
        }

        public bool Contains(global::CardDataSO cardSO)
        {
            if (cardSO == null)
                return false;

            foreach (GameObject cardPrefab in CurrentCards)
            {
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;

                if (cardData != null && cardData.DataSO == cardSO)
                    return true;
            }

            return false;
        }

        public bool TryFindByAiCardId(string aiCardId, out GameObject cardPrefab)
        {
            cardPrefab = null;

            if (string.IsNullOrWhiteSpace(aiCardId))
                return false;

            foreach (GameObject candidate in CurrentCards)
            {
                global::CardData cardData = candidate != null
                    ? candidate.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO != null &&
                    string.Equals(dataSO.AiCardId, aiCardId, System.StringComparison.OrdinalIgnoreCase))
                {
                    cardPrefab = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool Consume(global::CardDataSO cardSO)
        {
            if (cardSO == null)
                return false;

            List<GameObject> cards = Application.isPlaying && runtimeCardsInitialized
                ? runtimeCards
                : startingCards;

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject cardPrefab = cards[i];
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;

                if (cardData == null || !IsSameCardSO(cardData.DataSO, cardSO))
                    continue;

                cards.RemoveAt(i);
                NotifyChanged();
                return true;
            }

            return false;
        }

        public bool ConsumeAiCardId(string aiCardId)
        {
            if (string.IsNullOrWhiteSpace(aiCardId))
                return false;

            List<GameObject> cards = Application.isPlaying && runtimeCardsInitialized
                ? runtimeCards
                : startingCards;

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject cardPrefab = cards[i];
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO == null ||
                    !string.Equals(dataSO.AiCardId, aiCardId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cards.RemoveAt(i);
                NotifyChanged();
                return true;
            }

            return false;
        }

        private void NotifyChanged()
        {
            version++;
            Changed?.Invoke();
            AnyChanged?.Invoke(this);
        }

        public bool ContainsAiCardId(string aiCardId)
        {
            if (string.IsNullOrWhiteSpace(aiCardId))
                return false;

            foreach (GameObject cardPrefab in CurrentCards)
            {
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO != null &&
                    string.Equals(dataSO.AiCardId, aiCardId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameCardSO(global::CardDataSO a, global::CardDataSO b)
        {
            if (a == null || b == null)
                return false;

            if (a == b)
                return true;

            return !string.IsNullOrWhiteSpace(a.AiCardId)
                && string.Equals(a.AiCardId, b.AiCardId, System.StringComparison.OrdinalIgnoreCase);
        }

        private void TrimStartingCards()
        {
            if (startingCards == null)
                startingCards = new List<GameObject>();

            RemoveDuplicateCards(startingCards);

            if (startingCards.Count > MaxCards)
                startingCards.RemoveRange(MaxCards, startingCards.Count - MaxCards);
        }

        private static void RemoveDuplicateCards(List<GameObject> cards)
        {
            if (cards == null)
                return;

            for (int i = cards.Count - 1; i >= 0; i--)
            {
                GameObject card = cards[i];
                if (card == null)
                {
                    cards.RemoveAt(i);
                    continue;
                }

                for (int j = 0; j < i; j++)
                {
                    if (IsSameCard(cards[j], card))
                    {
                        cards.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private static bool AddUniqueCard(List<GameObject> cards, GameObject card)
        {
            if (cards == null || card == null || ContainsSameCard(cards, card))
                return false;

            cards.Add(card);
            return true;
        }

        private static bool ContainsSameCard(IEnumerable<GameObject> cards, GameObject card)
        {
            if (cards == null || card == null)
                return false;

            foreach (GameObject existing in cards)
            {
                if (IsSameCard(existing, card))
                    return true;
            }

            return false;
        }

        private static bool IsSameCard(GameObject a, GameObject b)
        {
            if (a == null || b == null)
                return false;

            if (ReferenceEquals(a, b))
                return true;

            global::CardData aData = a.GetComponent<global::CardData>();
            global::CardData bData = b.GetComponent<global::CardData>();
            global::CardDataSO aSO = aData != null ? aData.DataSO : null;
            global::CardDataSO bSO = bData != null ? bData.DataSO : null;

            if (aSO == null || bSO == null)
                return false;

            if (aSO == bSO)
                return true;

            return !string.IsNullOrWhiteSpace(aSO.AiCardId)
                && string.Equals(aSO.AiCardId, bSO.AiCardId, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
