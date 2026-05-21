using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDescPanel : ButtonPanel
{
    private GameManager gameManager = GameManager.Instance;
    private bool tierUIBindingAttempted;

    [Header("UI Elements")]
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text cardTitle;
    [SerializeField] private TMP_Text cardDesc;
    [Header("Tier UI")]
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private Image[] tierColorTargets;
    [SerializeField] private TierStyleSetSO tierStyleSet;
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

    private void Awake()
    {
        TryAutoBindTierUI();
    }

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
        if (!tierUIBindingAttempted)
        {
            TryAutoBindTierUI();
        }

        cardImage.sprite = data.CardImage;
        cardTitle.text = data.CardName;
        cardDesc.text = data.CardDescription;
        ApplyTierStyle(data.CardTier);

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

    private void TryAutoBindTierUI()
    {
        if (tierUIBindingAttempted)
        {
            return;
        }

        tierUIBindingAttempted = true;

        Transform mainSlot = transform.Find("MainSlot");
        if (mainSlot == null)
        {
            return;
        }

        Transform tierRoot = mainSlot.Find("Tier");
        Transform lineRoot = mainSlot.Find("Line");

        if (tierText == null && tierRoot != null)
        {
            tierText = tierRoot.GetComponentInChildren<TMP_Text>(true);
        }

        TrySetDefaultColorTargets(tierRoot, lineRoot);
    }

    private void ApplyTierStyle(Tier tier)
    {
        TierStyleData style = tierStyleSet != null ? tierStyleSet.GetStyle(tier) : null;
        Color targetColor = style != null ? style.color : Color.white;

        ApplyColorToTargets(targetColor);

        if (tierText != null)
        {
            tierText.text = style != null ? style.label : "미확인 등급";
        }
    }

    private void TrySetDefaultColorTargets(Transform tierRoot, Transform lineRoot)
    {
        if (tierColorTargets != null && tierColorTargets.Length > 0)
        {
            return;
        }

        int targetCount = 0;
        if (tierRoot != null && tierRoot.GetComponent<Image>() != null) targetCount++;
        if (lineRoot != null && lineRoot.GetComponent<Image>() != null) targetCount++;

        if (targetCount == 0)
        {
            return;
        }

        tierColorTargets = new Image[targetCount];
        int index = 0;

        if (tierRoot != null)
        {
            Image tierImage = tierRoot.GetComponent<Image>();
            if (tierImage != null)
            {
                tierColorTargets[index++] = tierImage;
            }
        }

        if (lineRoot != null)
        {
            Image line = lineRoot.GetComponent<Image>();
            if (line != null)
            {
                tierColorTargets[index] = line;
            }
        }
    }

    private void ApplyColorToTargets(Color color)
    {
        if (tierColorTargets == null)
        {
            return;
        }

        foreach (Image target in tierColorTargets)
        {
            if (target != null)
            {
                target.color = color;
            }
        }
    }
}
