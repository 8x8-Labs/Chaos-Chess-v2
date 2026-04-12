using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CardAnim : MonoBehaviour
{
    [SerializeField] private float startYPos;
    [SerializeField] private float clickYPos = 100;
    [SerializeField] private float clickYDuration = 0.3f;
    [SerializeField] private float duration;
    [SerializeField] private AudioClip clickSFX;
    [SerializeField] private Ease enableEase;
    [SerializeField] private Ease disableEase;

    private Ease disappearEase = Ease.InOutQuad;
    private Ease clickEase = Ease.OutCirc;
    private Image cardSprite;

    public CardData cardData { get; private set; } 
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
        SoundManager.Instance.SFXPlay("CardClickSFX", clickSFX);
        ClickOnAnimation();
        panel.SetCardData(this);
        panel.EnablePanel();
    }

    public void ClickOnAnimation()
    {
        cardSprite.rectTransform.DOAnchorPosY(clickYPos, clickYDuration).SetEase(clickEase);
    }
    public void ClickOffAnimation()
    {
        cardSprite.rectTransform.DOAnchorPosY(0f, clickYDuration).SetEase(clickEase);
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
