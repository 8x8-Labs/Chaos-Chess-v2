using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartDeckManager : MonoBehaviour
{
    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private Button rerollButton;
    [SerializeField] private int commonCardCount = 4;
    [SerializeField] private int uncommonCardCount = 2;
    [SerializeField] private int uniqueCardCount = 1;
    [SerializeField] private int maxRerollCount = 3;
    [SerializeField] private float rerollCardInterval = 0.12f;
    [SerializeField] private float rerollAnimationDurationMultiplier = 0.45f;
    [SerializeField] private float disabledTextAlpha = 0.5f;

    private readonly List<GameObject> currentStarterCards = new List<GameObject>();
    private TMP_Text rerollButtonText;
    private int remainingRerollCount;

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
        remainingRerollCount = Mathf.Max(0, maxRerollCount);
        UpdateRerollButtonText();
        SetRerollButtonInteractable(false);
        RemoveCurrentStarterCards();
        DealStarterCards();
    }

    public void Reroll()
    {
        if (remainingRerollCount <= 0) return;

        remainingRerollCount--;
        UpdateRerollButtonText();
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
        SetRerollButtonInteractable(remainingRerollCount > 0);
    }

    private void SetRerollButtonInteractable(bool interactable)
    {
        CacheRerollButton();
        if (rerollButton != null)
            rerollButton.interactable = interactable;

        SetRerollButtonTextAlpha(interactable ? 1f : disabledTextAlpha);
    }

    private void CacheRerollButton()
    {
        if (rerollButton != null)
        {
            CacheRerollButtonText();
            return;
        }

        StartDeckRerollButton marker = GetComponentInChildren<StartDeckRerollButton>(true);
        if (marker != null)
        {
            rerollButton = marker.Button;
            CacheRerollButtonText();
            return;
        }

        Debug.LogWarning($"{nameof(StartDeckManager)}에 {nameof(rerollButton)} 참조가 연결되지 않았습니다.", this);
    }

    private void CacheRerollButtonText()
    {
        if (rerollButtonText != null || rerollButton == null) return;

        rerollButtonText = rerollButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void UpdateRerollButtonText()
    {
        CacheRerollButtonText();
        if (rerollButtonText != null)
            rerollButtonText.text = $"다시 뽑기 {remainingRerollCount}/{Mathf.Max(0, maxRerollCount)}";
    }

    private void SetRerollButtonTextAlpha(float alpha)
    {
        CacheRerollButtonText();
        if (rerollButtonText == null) return;

        Color color = rerollButtonText.color;
        color.a = Mathf.Clamp01(alpha);
        rerollButtonText.color = color;
    }
}
