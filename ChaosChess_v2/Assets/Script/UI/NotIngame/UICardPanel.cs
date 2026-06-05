using System.Collections.Generic;
using System;
using DG.Tweening;
using UnityEngine;

public class UICardPanel : ButtonPanel
{
    public event Action CardSequenceCompleted;

    public UICardAnim[] anims;
    public int cardCount;

    [SerializeField] private float cardInterval = 0.2f;
    [SerializeField] private float panelCloseDelay = 0.5f;

    private readonly List<GameObject> cards = new List<GameObject>();
    private Sequence cardSequence;

    public void SetCards(List<GameObject> newCards)
    {
        cards.Clear();
        cards.AddRange(newCards);
    }

    public override void EnablePanel()
    {
        PrepareCards();
        base.EnablePanel();
        PlayCardSequence();
    }

    public override void DisablePanel()
    {
        base.DisablePanel();
    }

    public void RefreshCards()
    {
        PrepareCards();
        PlayCardSequence();
    }

    public void RefreshCards(float intervalOverride, float animationDurationMultiplier)
    {
        PrepareCards();
        PlayCardSequence(intervalOverride, animationDurationMultiplier);
    }

    private void PrepareCards()
    {
        cardCount = cards.Count;
        UICardAnim[] uiCards = anims;
        if (uiCards == null) return;

        for (int i = 0; i < uiCards.Length; i++)
        {
            if (uiCards[i] == null) continue;

            uiCards[i].gameObject.SetActive(false);
        }

        int count = Mathf.Min(cardCount, uiCards.Length);
        for (int i = 0; i < count; i++)
        {
            if (uiCards[i] == null) continue;

            uiCards[i].CardPreFab = cards[i];
        }
    }

    private void PlayCardSequence(float intervalOverride = -1f, float animationDurationMultiplier = 1f)
    {
        cardSequence?.Kill();
        cardSequence = DOTween.Sequence();
        if (anims == null)
        {
            cardSequence.AppendCallback(() => CardSequenceCompleted?.Invoke());
            return;
        }

        int count = Mathf.Min(cardCount, anims.Length);
        float interval = intervalOverride >= 0f ? intervalOverride : cardInterval;
        float lastAnimationDuration = 0f;

        for (int i = 0; i < count; i++)
        {
            UICardAnim anim = anims[i];
            if (anim == null) continue;

            cardSequence.AppendCallback(() =>
            {
                anim.gameObject.SetActive(true);
                anim.CardAnimation(animationDurationMultiplier);
            });

            lastAnimationDuration = Mathf.Max(0f, anim.Duration * animationDurationMultiplier);
            if (i < count - 1)
                cardSequence.AppendInterval(interval);
        }

        cardSequence.AppendInterval(lastAnimationDuration);
        cardSequence.AppendCallback(() => CardSequenceCompleted?.Invoke());

        if (panelCloseDelay >= 0f)
        {
            cardSequence.AppendInterval(panelCloseDelay);
            cardSequence.AppendCallback(DisablePanel);
        }
    }
}
