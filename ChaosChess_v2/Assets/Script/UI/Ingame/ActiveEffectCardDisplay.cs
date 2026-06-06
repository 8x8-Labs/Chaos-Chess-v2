using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ActiveEffectCardDisplay : MonoBehaviour
{
    [SerializeField] private RectTransform contentPanel;
    [SerializeField] private HorizontalLayoutGroup effectLayout;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float collapsedSpacing = -200f;
    [SerializeField] private float expandedSpacing = 10f;
    [SerializeField] private float spacingAnimDuration = 0.2f;
    [SerializeField] private ActiveEffectStatusTextSettings statusTextSettings = new();

    private readonly Dictionary<CardDataSO, ActiveEffectCard> cards = new();
    private ActiveEffectCard arenaCard;
    private bool isArenaCardDisplayed;
    private ArenaManager subscribedArenaManager;
    private Tween spacingTween;

    private void Awake()
    {
        RefreshTrayState();
    }

    private void OnEnable()
    {
        CleanUpDestroyedEffects();
        Effector.OnAnyEffectApplied += HandleEffectApplied;
        Effector.OnAnyEffectReverted += HandleEffectReverted;
        Effector.OnAnyEffectTurnTicked += HandleEffectTurnTicked;
        TrySubscribeArenaManager();
    }

    private void Start()
    {
        TrySubscribeArenaManager();
    }

    private void OnDisable()
    {
        Effector.OnAnyEffectApplied -= HandleEffectApplied;
        Effector.OnAnyEffectReverted -= HandleEffectReverted;
        Effector.OnAnyEffectTurnTicked -= HandleEffectTurnTicked;
        UnsubscribeArenaManager();
        spacingTween?.Kill();
    }

    public void ToggleCardSpread()
    {
        if (effectLayout == null || contentPanel == null || !contentPanel.gameObject.activeSelf)
            return;

        float targetSpacing = Mathf.Approximately(effectLayout.spacing, expandedSpacing)
            ? collapsedSpacing
            : expandedSpacing;

        spacingTween?.Kill();
        spacingTween = DOTween.To(
                () => effectLayout.spacing,
                spacing =>
                {
                    effectLayout.spacing = spacing;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel);
                },
                targetSpacing,
                spacingAnimDuration)
            .SetEase(Ease.OutQuad);
    }

    private void TrySubscribeArenaManager()
    {
        if (subscribedArenaManager != null) return;

        ArenaManager arenaManager = ArenaManager.Instance;
        if (arenaManager == null) return;

        subscribedArenaManager = arenaManager;
        subscribedArenaManager.ArenaStarted += HandleArenaStarted;
        subscribedArenaManager.ArenaRemainingTurnsChanged += HandleArenaRemainingTurnsChanged;
        subscribedArenaManager.ArenaEnded += HandleArenaEnded;
    }

    private void UnsubscribeArenaManager()
    {
        if (subscribedArenaManager == null) return;

        subscribedArenaManager.ArenaStarted -= HandleArenaStarted;
        subscribedArenaManager.ArenaRemainingTurnsChanged -= HandleArenaRemainingTurnsChanged;
        subscribedArenaManager.ArenaEnded -= HandleArenaEnded;
        subscribedArenaManager = null;
    }

    private void HandleEffectApplied(Effector effector)
    {
        CardDataSO cardSO = effector.CardSO;
        if (cardSO == null) return;

        bool isNewCard = false;
        if (!cards.TryGetValue(cardSO, out ActiveEffectCard card))
        {
            card = CreateCard(cardSO);
            cards.Add(cardSO, card);
            isNewCard = true;
        }

        card.Effects.Add(effector);
        UpdateCard(card);
        RefreshTrayState();

        if (isNewCard)
            card.CardUI?.PlayAppearAnimation();
    }

    private void CleanUpDestroyedEffects()
    {
        List<CardDataSO> keysToRemove = new();

        foreach (KeyValuePair<CardDataSO, ActiveEffectCard> pair in cards)
        {
            ActiveEffectCard card = pair.Value;
            card.Effects.RemoveAll(effect => effect == null);
            if (card.Effects.Count == 0)
            {
                if (card.Root != null)
                    Destroy(card.Root);

                keysToRemove.Add(pair.Key);
            }
            else
            {
                UpdateCard(card);
            }
        }

        foreach (CardDataSO cardSO in keysToRemove)
            cards.Remove(cardSO);

        RefreshTrayState();
    }

    private void HandleEffectReverted(Effector effector)
    {
        CardDataSO cardSO = effector.CardSO;
        if (cardSO == null) return;

        if (!cards.TryGetValue(cardSO, out ActiveEffectCard card)) return;

        card.Effects.Remove(effector);
        if (card.Effects.Count == 0)
        {
            cards.Remove(cardSO);
            if (card.CardUI != null)
            {
                card.CardUI.PlayDisappearAnimation(() =>
                {
                    Destroy(card.Root);
                    RefreshTrayState();
                });
            }
            else
            {
                Destroy(card.Root);
                RefreshTrayState();
            }

            return;
        }
        else
        {
            UpdateCard(card);
        }

        RefreshTrayState();
    }

    private void HandleEffectTurnTicked(Effector effector)
    {
        CardDataSO cardSO = effector.CardSO;
        if (cardSO == null || !cards.TryGetValue(cardSO, out ActiveEffectCard card)) return;

        UpdateCard(card);
    }

    private void HandleArenaStarted(CardDataSO cardSO, int remainingTurns)
    {
        if (isArenaCardDisplayed)
            RemoveArenaCard();

        arenaCard = CreateCard(
            cardSO,
            cardSO?.CardName,
            statusTextSettings.Format(ActiveEffectStatusType.TurnBased, remainingTurns));
        isArenaCardDisplayed = true;
        RefreshTrayState();
        arenaCard.CardUI?.PlayAppearAnimation();
    }

    private void HandleArenaRemainingTurnsChanged(int remainingTurns)
    {
        if (!isArenaCardDisplayed) return;

        arenaCard.CardUI.RemainTurn.text = statusTextSettings.Format(ActiveEffectStatusType.TurnBased, remainingTurns);
    }

    private void HandleArenaEnded()
    {
        if (!isArenaCardDisplayed) return;

        RemoveArenaCard();
    }

    private void RemoveArenaCard()
    {
        GameObject root = arenaCard.Root;
        if (arenaCard.CardUI != null)
        {
            arenaCard.CardUI.PlayDisappearAnimation(() =>
            {
                Destroy(root);
                RefreshTrayState();
            });
        }
        else
        {
            Destroy(root);
            RefreshTrayState();
        }

        arenaCard = null;
        isArenaCardDisplayed = false;
    }

    private ActiveEffectCard CreateCard(CardDataSO cardSO, string fallbackTitle = null, string status = null)
    {
        GameObject root = Instantiate(cardPrefab, contentPanel);
        root.name = fallbackTitle ?? cardSO?.CardName ?? "ActiveEffectCard";

        ActiveEffectCardUI cardUI = root.GetComponent<ActiveEffectCardUI>();
        if (cardUI != null)
        {
            if (cardSO != null)
                cardUI.CardImage.sprite = cardSO.CardImage;

            cardUI.Title.text = fallbackTitle;
            cardUI.RemainTurn.text = status ?? statusTextSettings.Format(ActiveEffectStatusType.Active);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel);

        return new ActiveEffectCard
        {
            Root = root,
            CardUI = cardUI
        };
    }

    private void RefreshTrayState()
    {
        int activeCount = cards.Count + (isArenaCardDisplayed ? 1 : 0);
        contentPanel?.gameObject.SetActive(activeCount > 0);

        if (activeCount == 0)
        {
            spacingTween?.Kill();
            if (effectLayout != null)
                effectLayout.spacing = collapsedSpacing;
        }

        if (contentPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel);
    }

    private void UpdateCard(ActiveEffectCard card)
    {
        if (card.CardUI == null) return;

        Effector representative = card.Effects
            .OrderBy(effect => effect.IsPermanent ? int.MaxValue : effect.RemainingTurns)
            .FirstOrDefault();

        card.CardUI.RemainTurn.text = GetStatusText(representative);
    }

    private string GetStatusText(Effector effector)
    {
        if (effector == null)
            return statusTextSettings.Format(ActiveEffectStatusType.Active);

        ActiveEffectStatusType statusType = effector.CardSO != null
            ? effector.CardSO.StatusDisplayType
            : ActiveEffectStatusType.Active;

        if (statusType is ActiveEffectStatusType.TurnBased or ActiveEffectStatusType.CountBased)
            return statusTextSettings.Format(statusType, effector.RemainingTurns);

        return statusTextSettings.Format(statusType);
    }

    private sealed class ActiveEffectCard
    {
        public GameObject Root;
        public ActiveEffectCardUI CardUI;
        public readonly List<Effector> Effects = new();
    }
}
