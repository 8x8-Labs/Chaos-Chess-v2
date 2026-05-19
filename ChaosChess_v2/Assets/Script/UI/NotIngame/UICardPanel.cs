using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class UICardPanel : ButtonPanel
{
    public UICardAnim[] anims;
    public int cardCount;

    [SerializeField] private float cardInterval = 0.2f;
    [SerializeField] private float panelCloseDelay = 0.5f;

    private CardRandomizerManager cardRandomizerManager = CardRandomizerManager.Instance;
    private Sequence cardSequence;
    private int requestedCardCount;

    public void SetRequestedCardCount(int count)
    {
        requestedCardCount = count;
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

    public UICardAnim[] GetUICards => anims;

    private void PrepareCards()
    {
        if (PlayerState.Instance == null || cardRandomizerManager == null)
            return;

        List<GameObject> ownedCards = PlayerState.Instance.CardPool.ToList();
        List<GameObject> randomCards =
            cardRandomizerManager.GetRandomCardsFromAll(
                ownedCards,
                requestedCardCount
            );

        cardCount = randomCards.Count;
        UICardAnim[] uiCards = GetUICards;

        for (int i = 0; i < uiCards.Length; i++)
        {
            uiCards[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < cardCount; i++)
        {
            uiCards[i].CardPreFab = randomCards[i];
        }
    }

    private void PlayCardSequence()
    {
        cardSequence?.Kill();
        cardSequence = DOTween.Sequence();

        for (int i = 0; i < cardCount; i++)
        {
            int index = i;
            cardSequence.AppendCallback(() =>
            {
                anims[index].gameObject.SetActive(true);
                anims[index].CardAnimation();
            });
            cardSequence.AppendInterval(cardInterval);
        }

        cardSequence.AppendInterval(panelCloseDelay);
        cardSequence.AppendCallback(DisablePanel);
    }
}
