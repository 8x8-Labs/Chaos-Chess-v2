using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICardDescPanel : ButtonPanel
{
    private GameManager gameManager = GameManager.Instance;

    [Header("UI Elements")]
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text cardTitle;
    [SerializeField] private TMP_Text cardDesc;

    public override void DisablePanel()
    {
        base.DisablePanel();
    }

    public override void EnablePanel()
    {
        base.EnablePanel();
    }

    public void SetCardData(CardData data)
    {
        UpdateUI(data.DataSO);
    }

    private void UpdateUI(CardDataSO data)
    {
        cardImage.sprite = data.CardImage;
        cardTitle.text = data.CardName;
        cardDesc.text = data.CardDescription;
    }
}
