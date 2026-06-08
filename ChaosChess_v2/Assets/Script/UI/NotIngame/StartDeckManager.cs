using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartDeckManager : MonoBehaviour
{
    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private Button rerollButton;
    [SerializeField] private int commonCardCount = 4;
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
        DealStarterCards();
    }

    public void Reroll()
    {
        SetRerollButtonInteractable(false);
        RemoveCurrentStarterCards();
        DealStarterCards();
        uiCardPanel?.RefreshCards(rerollCardInterval, rerollAnimationDurationMultiplier);
    }

    private void DealStarterCards()
    {
        CardRandomizerManager randomizer = CardRandomizerManager.Instance;
        PlayerState playerState = PlayerState.Instance;
        if (randomizer == null || playerState == null) return;

        List<GameObject> starterCards = GenerateStarterCards(randomizer);

        foreach (var card in starterCards)
        {
            playerState.AddCard(card);
        }

        currentStarterCards.Clear();
        currentStarterCards.AddRange(starterCards);
        uiCardPanel?.SetCards(starterCards);
    }

    private List<GameObject> GenerateStarterCards(CardRandomizerManager randomizer)
    {
        List<GameObject> starterCards = new List<GameObject>();
        AddCardsByTier(starterCards, randomizer, Tier.Common, commonCardCount);
        AddCardsByTier(starterCards, randomizer, Tier.Uncommon, uncommonCardCount);
        AddCardsByTier(starterCards, randomizer, Tier.Unique, uniqueCardCount);
        return starterCards;
    }

    private void AddCardsByTier(
        List<GameObject> destination,
        CardRandomizerManager randomizer,
        Tier tier,
        int count)
    {
        if (count <= 0) return;

        destination.AddRange(randomizer.GetRandomCardsByTier(tier, count));
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

        StartDeckRerollButton marker = GetComponentInChildren<StartDeckRerollButton>(true);
        if (marker != null)
        {
            rerollButton = marker.Button;
            return;
        }

        Debug.LogWarning($"{nameof(StartDeckManager)}에 {nameof(rerollButton)} 참조가 연결되지 않았습니다.", this);
    }
}
