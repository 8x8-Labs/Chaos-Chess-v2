using System;
using System.Collections.Generic;
using ChaosChess.AI.Domain;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Mapping
{
    public static class UnityCardInfoMapper
    {
        public static IReadOnlyList<CardInfo> MapCards(
            IEnumerable<GameObject> cardPrefabs,
            int remainingUses,
            IList<string> warnings)
        {
            var cards = new List<CardInfo>();

            if (cardPrefabs == null)
                return cards.AsReadOnly();

            if (remainingUses < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingUses), remainingUses, "Remaining uses cannot be negative.");

            foreach (GameObject prefab in cardPrefabs)
            {
                if (TryMapCard(prefab, remainingUses, warnings, out CardInfo card))
                    cards.Add(card);
            }

            return cards.AsReadOnly();
        }

        public static bool TryMapCard(
            GameObject cardPrefab,
            int remainingUses,
            IList<string> warnings,
            out CardInfo card)
        {
            card = null;

            if (cardPrefab == null)
            {
                AddWarning(warnings, "Skipped null card prefab.");
                return false;
            }

            global::CardData cardData = cardPrefab.GetComponent<global::CardData>();
            if (cardData == null || cardData.DataSO == null)
            {
                AddWarning(warnings, $"Skipped card prefab '{cardPrefab.name}' because it has no CardData/DataSO.");
                return false;
            }

            return TryMapCardData(cardData.DataSO, remainingUses, warnings, out card);
        }

        public static bool TryMapCardData(
            global::CardDataSO dataSO,
            int remainingUses,
            IList<string> warnings,
            out CardInfo card)
        {
            card = null;

            if (dataSO == null)
            {
                AddWarning(warnings, "Skipped null CardDataSO.");
                return false;
            }

            if (remainingUses < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingUses), remainingUses, "Remaining uses cannot be negative.");

            if (string.IsNullOrWhiteSpace(dataSO.AiCardId))
            {
                AddWarning(warnings, $"Skipped card '{dataSO.CardName}' because AiCardId is empty.");
                return false;
            }

            if (dataSO.AiCategory == global::AiCardCategory.Unknown)
            {
                AddWarning(warnings, $"Skipped card '{dataSO.CardName}' because AiCategory is Unknown.");
                return false;
            }

            card = new CardInfo(dataSO.AiCardId, dataSO.AiCategory.ToString(), remainingUses);
            return true;
        }

        private static void AddWarning(IList<string> warnings, string message)
        {
            warnings?.Add(message);
        }
    }
}
