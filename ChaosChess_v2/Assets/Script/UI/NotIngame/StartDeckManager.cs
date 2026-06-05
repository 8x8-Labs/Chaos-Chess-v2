using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartDeckManager : MonoBehaviour
{
    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private Button rerollButton;
    [SerializeField] private int commonCardCount = 2;
    [SerializeField] private int uncommonCardCount = 2;
    [SerializeField] private int uniqueCardCount = 1;
    [SerializeField] private float rerollCardInterval = 0.12f;
    [SerializeField] private float rerollAnimationDurationMultiplier = 0.45f;

    private readonly List<GameObject> currentStarterCards = new List<GameObject>();

    private void OnEnable()
    {
        if (uiCardPanel != null)
            uiCardPanel.CardSequenceCompleted += EnableRerollButton;
    }

    private void OnDisable()
    {
        if (uiCardPanel != null)
            uiCardPanel.CardSequenceCompleted -= EnableRerollButton;
    }

    public void Init()
    {
        CacheRerollButton();
        SetRerollButtonInteractable(false);
        RemoveCurrentStarterCards();
        DealStarterCards(null);
    }

    public void Reroll()
    {
        SetRerollButtonInteractable(false);
        List<GameObject> previousCards = new List<GameObject>(currentStarterCards);
        RemoveCurrentStarterCards();
        DealStarterCards(previousCards);
        uiCardPanel?.RefreshCards(rerollCardInterval, rerollAnimationDurationMultiplier);
    }

    private void DealStarterCards(List<GameObject> excludedCards)
    {
        CardRandomizerManager randomizer = CardRandomizerManager.Instance;
        if (randomizer == null) return;

        List<GameObject> starterCards = GenerateStarterCards(randomizer, excludedCards);

        foreach (var card in starterCards)
        {
            PlayerState.Instance?.AddCard(card);
        }

        currentStarterCards.Clear();
        currentStarterCards.AddRange(starterCards);
        uiCardPanel.SetCards(starterCards);
    }

    private List<GameObject> GenerateStarterCards(CardRandomizerManager randomizer, List<GameObject> excludedCards)
    {
        List<GameObject> starterCards = new List<GameObject>();
        AddCardsByTier(starterCards, randomizer, Tier.Common, commonCardCount, excludedCards);
        AddCardsByTier(starterCards, randomizer, Tier.Uncommon, uncommonCardCount, excludedCards);
        AddCardsByTier(starterCards, randomizer, Tier.Unique, uniqueCardCount, excludedCards);
        return starterCards;
    }

    private void AddCardsByTier(
        List<GameObject> destination,
        CardRandomizerManager randomizer,
        Tier tier,
        int count,
        List<GameObject> excludedCards)
    {
        if (count <= 0) return;

        List<GameObject> pickedCards = randomizer.GetRandomCardsByTier(tier, count, excludedCards);
        if (pickedCards.Count < count && excludedCards != null && excludedCards.Count > 0)
        {
            pickedCards.AddRange(randomizer.GetRandomCardsByTier(tier, count - pickedCards.Count, pickedCards));
        }

        destination.AddRange(pickedCards);
    }

    private void RemoveCurrentStarterCards()
    {
        if (PlayerState.Instance == null) return;

        foreach (GameObject card in currentStarterCards)
        {
            PlayerState.Instance.RemoveCard(card);
        }

        currentStarterCards.Clear();
    }

    private void EnableRerollButton()
    {
        SetRerollButtonInteractable(true);
    }

    private void SetRerollButtonInteractable(bool interactable)
    {
        CacheRerollButton();
        if (rerollButton != null)
            rerollButton.interactable = interactable;
    }

    private void CacheRerollButton()
    {
        if (rerollButton != null) return;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == "RerollButton")
            {
                rerollButton = button;
                return;
            }
        }
    }
}
