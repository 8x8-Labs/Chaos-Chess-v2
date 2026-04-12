using System;
using System.Security;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDescPanel : ButtonPanel
{
    private GameManager gameManager = GameManager.Instance;

    [Header("UI Elements")]
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text cardTitle;
    [SerializeField] private TMP_Text cardDesc;
    [SerializeField] private Button executeButton;

    private CardAnim selectedCard;

    public override void DisablePanel()
    {
        if(selectedCard != null)
        {
            selectedCard.ClickOffAnimation();
            selectedCard = null;
        }
        base.DisablePanel();
        if( gameManager == null ) gameManager = GameManager.Instance;
        gameManager.IsGameInput = true;
    }

    public override void EnablePanel()
    {
        base.EnablePanel();
        if( gameManager == null ) gameManager = GameManager.Instance;
        gameManager.IsGameInput = false;
    }

    public void SetCardData(CardAnim card)
    {
        selectedCard = card;
        CardData data = card.cardData;

        UpdateUI(data.DataSO);

        executeButton.onClick.RemoveAllListeners();
        Action cardExecute = null;

        CardType type = data.DataSO.Type;
        var pieceCard = data.GetComponent<IPieceCard>();
        var tileCard = data.GetComponent<ITileCard>();
        var cardInterface = data.GetComponent<ICard>();

        switch(type){
            case CardType.Piece:
                cardExecute = () => pieceCard?.LoadPieceSelector();
                break;
            case CardType.Tile:
                cardExecute = () => tileCard?.LoadTileSelector();
                break;
            case CardType.Global:
                cardExecute = () =>
                {
                    cardInterface?.Execute();
                    FindFirstObjectByType<CardRandomizer>()?.RemoveCard(data.gameObject);
                };
                break;
        }

        executeButton.onClick.AddListener(() =>
            {
                DisablePanel();
                cardExecute?.Invoke();
            }
        );
    }

    private void UpdateUI(CardDataSO data)
    {
        cardImage.sprite = data.CardImage;
        cardTitle.text = data.CardName;
        cardDesc.text = data.CardDescription;
    }
}
