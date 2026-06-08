using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CardBookTierGroup : MonoBehaviour
{
    [SerializeField] private Tier tier;
    [SerializeField] private ScrollRect scrollRect;

    private CardDescriptionUI descriptionUI;

    public Tier Tier => tier;

    public void SetDescriptionUI(CardDescriptionUI ui) => descriptionUI = ui;

    private UICardData[] cardItems;
    private bool dataAssigned;

    private void Awake()
    {
        cardItems = GetComponentsInChildren<UICardData>(true);
    }

    private void Start()
    {
        AssignCardData();
    }

    private void AssignCardData()
    {
        if (dataAssigned || CardRandomizerManager.Instance == null || cardItems == null) return;

        List<CardDataSO> tierCards = new();
        foreach (GameObject cardGO in CardRandomizerManager.Instance.AllCards)
        {
            if (cardGO == null) continue;
            CardData data = cardGO.GetComponent<CardData>();
            if (data?.DataSO != null && data.DataSO.CardTier == tier)
                tierCards.Add(data.DataSO);
        }

        Debug.Log($"CardBookTierGroup[{tier}]: 슬롯 {cardItems.Length}개, 등록된 카드 {tierCards.Count}개 - {string.Join(", ", tierCards.Select(c => c.CardName))}");

        for (int i = 0; i < cardItems.Length; i++)
        {
            if (cardItems[i] == null) continue;
            if (i < tierCards.Count)
                cardItems[i].Init(tierCards[i], descriptionUI);
        }

        dataAssigned = true;
    }

    public void ResetScroll()
    {
        if (scrollRect != null)
            scrollRect.horizontalNormalizedPosition = 0f;
    }

    public void RefreshStates()
    {
        if (cardItems == null) return;

        AssignCardData();

        foreach (UICardData item in cardItems)
        {
            if (item == null) continue;
            bool discovered = CollectionManager.Instance != null &&
                              CollectionManager.Instance.IsDiscovered(item.CardName);
            item.SetDiscovered(discovered);
        }
    }
}
