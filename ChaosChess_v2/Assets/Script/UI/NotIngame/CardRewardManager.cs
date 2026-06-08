using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardRewardManager : MonoBehaviour
{
    private static readonly Tier[] AllTiers =
    {
        Tier.Common,
        Tier.Uncommon,
        Tier.Unique,
        Tier.Rare,
        Tier.Legendary
    };

    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private TextMeshProUGUI cardRewardText;

    [Header("Tier Weights")]
    [SerializeField, Min(0f)] private float commonWeight = 50f;
    [SerializeField, Min(0f)] private float uncommonWeight = 25f;
    [SerializeField, Min(0f)] private float uniqueWeight = 15f;
    [SerializeField, Min(0f)] private float rareWeight = 8f;
    [SerializeField, Min(0f)] private float legendaryWeight = 2f;

    private void Start()
    {
        if (PlayerState.Instance == null || CardRandomizerManager.Instance == null)
            return;

        int rewardCardCount = ResolveRewardCardCount();
        if (rewardCardCount <= 0 || uiCardPanel == null)
            return;

        List<GameObject> ownedCards = new List<GameObject>(PlayerState.Instance.CardPool);
        List<GameObject> rewardCards = GetRewardCards(ownedCards, rewardCardCount);

        foreach (var card in rewardCards)
        {
            PlayerState.Instance?.AddCard(card);
        }

        uiCardPanel.SetCards(rewardCards);

        if (cardRewardText != null)
            cardRewardText.text = $"카드 {rewardCardCount}개 얻기";
    }

    private List<GameObject> GetRewardCards(List<GameObject> ownedCards, int count)
    {
        List<GameObject> rewardCards = new List<GameObject>();
        HashSet<GameObject> excludedCards = new HashSet<GameObject>(ownedCards);

        for (int i = 0; i < count; i++)
        {
            List<Tier> availableTiers = new List<Tier>(AllTiers);

            while (availableTiers.Count > 0)
            {
                Tier tier = RollTier(availableTiers);
                if (CardRandomizerManager.Instance.TryGetRandomCardByTier(
                        tier,
                        excludedCards,
                        out GameObject card))
                {
                    rewardCards.Add(card);
                    excludedCards.Add(card);
                    break;
                }

                availableTiers.Remove(tier);
            }
        }

        return rewardCards;
    }

    private Tier RollTier(List<Tier> availableTiers)
    {
        float totalWeight = 0f;
        foreach (Tier tier in availableTiers)
        {
            totalWeight += GetTierWeight(tier);
        }

        // 모든 가중치가 0이거나 음수인 경우, 확률을 동일하게 설정 후 반환
        if (totalWeight <= 0f)
            return availableTiers[Random.Range(0, availableTiers.Count)];


        float roll = Random.value * totalWeight;
        float cumulativeWeight = 0f;
        Tier selectedTier = availableTiers[0];

        foreach (Tier tier in availableTiers)
        {
            float weight = GetTierWeight(tier);
            if (weight <= 0f)
                continue;

            selectedTier = tier;
            cumulativeWeight += weight;
            if (roll < cumulativeWeight)
                break;
        }

        return selectedTier;
    }

    private float GetTierWeight(Tier tier)
    {
        return tier switch
        {
            Tier.Common => commonWeight,
            Tier.Uncommon => uncommonWeight,
            Tier.Unique => uniqueWeight,
            Tier.Rare => rareWeight,
            Tier.Legendary => legendaryWeight,
            _ => 0f
        };
    }

    private int ResolveRewardCardCount()
    {
        if (PlayerState.Instance.CurGameResult == GameResult.WhiteWin)
        {
            bool isEliteClear = MapManager.Instance?.curMap?.nodeType == NodeType.Elite;
            return isEliteClear ? 6 : 3;
        }

        if (PlayerState.Instance.CurGameResult == GameResult.Draw)
            return 1;

        return 0;
    }
}
