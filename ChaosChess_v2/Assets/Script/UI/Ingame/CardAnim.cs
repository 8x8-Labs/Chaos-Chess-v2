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
    [SerializeField] private AudioClip appearSFX;
    [SerializeField] private Ease enableEase;
    [SerializeField] private Ease disableEase;

    private Ease disappearEase = Ease.InOutQuad;
    private Ease clickEase = Ease.OutCirc;
    private Image cardSprite;
    private RectTransform rt;
    private float originalWidth;

    public CardData cardData { get; private set; }
    private CardDescPanel panel;

    private void Awake()
    {
        cardSprite = GetComponentInChildren<Image>();
        cardData = GetComponent<CardData>();
        panel = FindObjectOfType<CardDescPanel>();

        rt = GetComponent<RectTransform>();
        originalWidth = rt.sizeDelta.x;
        rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y);

        ApplyCardSprite();
        CardAnimation();
    }

    private void ApplyCardSprite()
    {
        cardSprite.sprite = cardData?.DataSO?.CardImage;
    }

    private void CardAnimation()
    {
        if (SoundManager.Instance != null && appearSFX != null)
            SoundManager.Instance.SFXPlay("CardAppear", appearSFX);

        cardSprite.rectTransform.anchoredPosition = new Vector3(0, startYPos, 0);

        DOTween.Sequence()
            .Join(rt.DOSizeDelta(new Vector2(originalWidth, rt.sizeDelta.y), duration).SetEase(enableEase))
            .Join(cardSprite.rectTransform.DOAnchorPosY(0f, duration).SetEase(enableEase));
    }

    public void EnableCardDataUI()
    {
        if (CardSelectionState.IsLocked)
            return;

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
