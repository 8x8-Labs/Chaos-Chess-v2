using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class UICardPanel : ButtonPanel
{
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

    private void PrepareCards()
    {
        cardCount = cards.Count;
        UICardAnim[] uiCards = anims;

        for (int i = 0; i < uiCards.Length; i++)
        {
            uiCards[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < cardCount; i++)
        {
            uiCards[i].CardPreFab = cards[i];
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

        if (panelCloseDelay >= 0f)
        {
            cardSequence.AppendInterval(panelCloseDelay);
            cardSequence.AppendCallback(DisablePanel);
        }
    }
}
