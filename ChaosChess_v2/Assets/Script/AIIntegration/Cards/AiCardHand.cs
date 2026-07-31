using System.Collections.Generic;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public sealed class AiCardHand : MonoBehaviour
    {
        [SerializeField] private List<GameObject> startingCards = new();
        [SerializeField] private int defaultRemainingUses = 1;

        public IReadOnlyList<GameObject> AvailableCards => startingCards;
        public int DefaultRemainingUses => Mathf.Max(0, defaultRemainingUses);

        public bool Contains(global::CardDataSO cardSO)
        {
            if (cardSO == null)
                return false;

            foreach (GameObject cardPrefab in startingCards)
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

            foreach (GameObject candidate in startingCards)
            {
                global::CardData cardData = candidate != null
                    ? candidate.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO != null && dataSO.AiCardId == aiCardId)
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

            for (int i = 0; i < startingCards.Count; i++)
            {
                GameObject cardPrefab = startingCards[i];
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;

                if (cardData == null || cardData.DataSO != cardSO)
                    continue;

                startingCards.RemoveAt(i);
                return true;
            }

            return false;
        }
    }
}
