using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CardAnim : MonoBehaviour
{
    [SerializeField] private float startYPos;
    [SerializeField] private float duration;
    [SerializeField] private Ease enableEase;
    [SerializeField] private Ease disableEase;

    private Ease disappearEase = Ease.InOutQuad;
    private Image cardSprite;

    private CardData cardData;
    private CardDescPanel panel;

    private void Awake()
    {
        cardSprite = GetComponentInChildren<Image>();
        cardData = GetComponent<CardData>();
        panel = FindObjectOfType<CardDescPanel>();
        CardAnimation();
    }

    private void CardAnimation()
    {
        cardSprite.rectTransform.anchoredPosition = new Vector3(0, startYPos, 0);
        cardSprite.rectTransform.DOAnchorPosY(0f, duration).SetEase(enableEase);
    }

    public void EnableCardDataUI()
    {
        panel.SetCardData(cardData);
        panel.EnablePanel();
    }

    public void DestroyCard()
    {
        var rt = GetComponent<RectTransform>();
        cardSprite.rectTransform.DOAnchorPosY(startYPos, duration / 2f)
            .SetEase(disableEase)
            .OnComplete(
            () =>
            {
                rt.DOSizeDelta(new Vector2(0, rt.sizeDelta.y), 0.2f)
                    .SetEase(disappearEase)
                    .OnComplete(() => Destroy(gameObject));
            });
    }
}
