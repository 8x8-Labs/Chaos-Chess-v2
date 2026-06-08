using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICardAnim : MonoBehaviour
{
    [SerializeField] private float startYPos;
    [SerializeField] private float duration;
    [SerializeField] private Ease enableEase;
    [SerializeField] private Ease disableEase;

    [SerializeField] private float fadeDuration = 0.1f;

    private Image cardSprite;
    private CanvasGroup canvasGroup;
    private RectTransform rt;
    private float originalWidth;
    public float Duration => duration;

    [SerializeField] private GameObject cardPrefab;
    public GameObject CardPreFab { get { return cardPrefab; } set { cardPrefab = value; } }

    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private Image cardImage;

    private void Awake()
    {
        cardSprite = GetComponentInChildren<Image>();
        cardSprite.rectTransform.anchoredPosition = new Vector3(0, startYPos, 0);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        rt = GetComponent<RectTransform>();
        originalWidth = rt.sizeDelta.x;
        rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y);

        gameObject.SetActive(false);
    }

    public void CardAnimation(float durationMultiplier = 1f)
    {
        float adjustedDuration = Mathf.Max(0.01f, duration * durationMultiplier);
        float adjustedFadeDuration = Mathf.Max(0.01f, fadeDuration * durationMultiplier);

        rt.DOKill();
        canvasGroup.DOKill();
        cardSprite.rectTransform.DOKill();

        rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y);
        canvasGroup.alpha = 0f;
        cardSprite.rectTransform.anchoredPosition = new Vector3(0, startYPos, 0);

        DOTween.Sequence()
            .Append(rt.DOSizeDelta(new Vector2(originalWidth, rt.sizeDelta.y), adjustedFadeDuration).SetEase(enableEase))
            .Join(canvasGroup.DOFade(1f, adjustedFadeDuration).SetEase(enableEase))
            .Join(cardSprite.rectTransform.DOAnchorPosY(0f, adjustedDuration).SetEase(enableEase));

        if (cardPrefab != null)
        {
            CardData cardDataComp = cardPrefab.GetComponent<CardData>();
            if (cardDataComp != null)
            {
                CardDataSO dataSO = cardDataComp.DataSO;
                if (cardNameText != null)
                    cardNameText.text = dataSO?.CardName ?? cardNameText.text;
                if (cardImage != null)
                    cardImage.sprite = dataSO?.CardImage ?? cardImage.sprite;
            }
        }
    }
}
