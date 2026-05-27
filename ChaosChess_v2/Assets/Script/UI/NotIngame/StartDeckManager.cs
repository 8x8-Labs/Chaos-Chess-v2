using System.Collections.Generic;
using UnityEngine;

public class StartDeckManager : MonoBehaviour
{
    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private int commonCardCount = 2;
    [SerializeField] private int uncommonCardCount = 1;
    [SerializeField] private int uniqueCardCount = 1;

    public void Init()
    {
        CardRandomizerManager randomizer = CardRandomizerManager.Instance;
        if (randomizer == null) return;

        List<GameObject> starterCards = new List<GameObject>();
        starterCards.AddRange(randomizer.GetRandomCardsByTier(Tier.Common, commonCardCount));
        starterCards.AddRange(randomizer.GetRandomCardsByTier(Tier.Uncommon, uncommonCardCount));
        starterCards.AddRange(randomizer.GetRandomCardsByTier(Tier.Unique, uniqueCardCount));

        foreach (var card in starterCards)
        {
            PlayerState.Instance?.AddCard(card);
        }

        uiCardPanel.SetCards(starterCards);
    }
}
