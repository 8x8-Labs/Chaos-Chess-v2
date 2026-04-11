using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CardAnim : MonoBehaviour
{
    [SerializeField] private float startYPos;
    [SerializeField] private float duration;
    [SerializeField] private Ease ease;
    private Image cardSprite;

    private CardData cardData;
    private CardDescPanel panel;

    private void Start()
    {
        cardSprite = GetComponentInChildren<Image>();
        cardData = GetComponent<CardData>();
        panel = FindObjectOfType<CardDescPanel>();
        CardAnimation();
    }

    private void CardAnimation()
    {
        cardSprite.rectTransform.anchoredPosition = new Vector3(0, startYPos, 0);
        cardSprite.rectTransform.DOAnchorPosY(0f, duration).SetEase(ease);
    }

    public void EnableCardDataUI()
    {
        panel.SetCardData(cardData);
        panel.EnablePanel();
    }
}
