using System.Collections.Generic;
using UnityEngine;

public class CardBookTierGroup : MonoBehaviour
{
    [SerializeField] private Tier tier;

    private CardDescriptionUI descriptionUI;

    public Tier Tier => tier;

    public void SetDescriptionUI(CardDescriptionUI ui) => descriptionUI = ui;

    private UICardData[] cardItems;

    private void Start()
    {
        cardItems = GetComponentsInChildren<UICardData>(true);
        AssignCardData();
    }

    private void AssignCardData()
    {
        if (CardRandomizerManager.Instance == null || cardItems == null) return;

        List<CardDataSO> tierCards = new();
        foreach (GameObject cardGO in CardRandomizerManager.Instance.AllCards)
        {
            CardData data = cardGO.GetComponent<CardData>();
            if (data?.DataSO != null && data.DataSO.CardTier == tier)
                tierCards.Add(data.DataSO);
        }

        for (int i = 0; i < cardItems.Length; i++)
        {
            if (cardItems[i] == null) continue;
            if (i < tierCards.Count)
                cardItems[i].Init(tierCards[i], descriptionUI);
        }
    }

    public void RefreshStates()
    {
        if (cardItems == null) return;

        foreach (UICardData item in cardItems)
        {
            if (item == null) continue;
            bool discovered = CollectionManager.Instance != null &&
                              CollectionManager.Instance.IsDiscovered(item.CardName);
            item.SetDiscovered(discovered);
        }
    }
}
