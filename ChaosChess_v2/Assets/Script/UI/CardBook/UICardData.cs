using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UIButton))]
public class UICardData : MonoBehaviour
{
    [SerializeField] private CardDataSO cardData;
    [SerializeField] private CardDescriptionUI descriptionUI;
    [SerializeField] private Sprite lockedSprite;

    private TMP_Text cardTitle;
    private Image cardImage;
    private UIButton button;

    public string CardName => cardData != null ? cardData.CardName : string.Empty;

    private void Awake()
    {
        cardTitle = GetComponentInChildren<TMP_Text>(true);
        cardImage = System.Array.Find(GetComponentsInChildren<Image>(true), img => img.gameObject != gameObject);
        button = GetComponent<UIButton>();
        button?.onClick.AddListener(ClickEvent);
    }

    private void Start()
    {
        if (cardData == null) return;
        if (cardTitle != null) cardTitle.text = cardData.CardName;
        if (cardImage != null) cardImage.sprite = cardData.CardImage;
    }

    public void Init(CardDataSO data, CardDescriptionUI desc)
    {
        cardData = data;
        descriptionUI = desc;
        if (cardTitle != null) cardTitle.text = data != null ? data.CardName : string.Empty;
        if (cardImage != null) cardImage.sprite = data != null ? data.CardImage : null;
    }

    public void SetDiscovered(bool discovered)
    {
        if (button != null) button.interactable = discovered;
        if (discovered)
        {
            if (cardImage != null) cardImage.sprite = cardData != null ? cardData.CardImage : null;
            if (cardTitle != null) cardTitle.text = cardData != null ? cardData.CardName : string.Empty;
        }
        else
        {
            if (cardImage != null) cardImage.sprite = lockedSprite;
            if (cardTitle != null) cardTitle.text = string.Empty;
        }
    }

    private void ClickEvent()
    {
        if (descriptionUI == null || cardData == null) return;
        descriptionUI.SetCardData(cardData);
        descriptionUI.EnablePanel();
    }
}
