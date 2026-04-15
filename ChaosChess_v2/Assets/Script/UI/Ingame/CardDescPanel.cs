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
    [SerializeField] private GameObject subDesc;
    [SerializeField] private TMP_Text subDescTitle;
    // 기물 타입
    [SerializeField] private Image subDescPieceImage;
    [SerializeField] private Image subDescMovementImage;
    [SerializeField] private TMP_Text subDescPieceContent;
    // 규칙 타입
    [SerializeField] private TMP_Text subDescContent;

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

        if (data.NeedAdditionalDescription)
        {
            subDesc.SetActive(true);
            if (subDescTitle != null)
                subDescTitle.text = data.AdditionalDescriptionTitle;

            bool isPiece = data.DescriptionType == AdditionalDescription.Piece;
            if (subDescPieceImage != null) subDescPieceImage.gameObject.SetActive(isPiece);
            if (subDescMovementImage != null) subDescMovementImage.gameObject.SetActive(isPiece);
            if (subDescPieceContent != null) subDescPieceContent.gameObject.SetActive(isPiece);
            if (subDescContent != null) subDescContent.gameObject.SetActive(!isPiece);

            if (isPiece)
            {
                if (subDescPieceImage != null)
                    subDescPieceImage.sprite = data.PieceDescImage;
                if (subDescMovementImage != null)
                    subDescMovementImage.sprite = data.MovementImage;
                if (subDescPieceContent != null)
                    subDescPieceContent.text = data.PieceDescContent;
            }
            else
            {
                if (subDescContent != null)
                    subDescContent.text = data.AdditionalDescriptionContent;
            }
        }
        else
        {
            subDesc.SetActive(false);
        }
    }
}
