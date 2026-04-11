using System;
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

    public override void DisablePanel()
    {
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

    public void SetCardData(CardData data)
    {
        UpdateUI(data.DataSO);

        executeButton.onClick.RemoveAllListeners();
        // 현재 추가된 인터페이스에 따라 실행을 변경(선택자 호출 or 즉발)

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
