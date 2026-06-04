using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UIButton))]
public class UICardData : MonoBehaviour
{
    [SerializeField] private CardDataSO cardData;
    [SerializeField] private TMP_Text cardTitle;
    [SerializeField] private Image cardImage;
    [SerializeField] private CardDescriptionUI descriptionUI;

    private UIButton button;

    private void Start()
    {
        button = GetComponent<UIButton>();
        button?.onClick.AddListener(ClickEvent);

        if (cardData == null) return;
        if (cardTitle != null) cardTitle.text = cardData.CardName;
        if (cardImage != null) cardImage.sprite = cardData.CardImage;
    }

    private void ClickEvent()
    {
        if (descriptionUI == null || cardData == null) return;
        descriptionUI.SetCardData(cardData);
        descriptionUI.EnablePanel();
    }
}
