using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UICardAnim : MonoBehaviour
{
    [SerializeField] private float startYPos;
    [SerializeField] private float duration;
    [SerializeField] private Ease enableEase;
    [SerializeField] private Ease disableEase;

    [SerializeField] private float fadeDuration = 0.1f;

    private Ease disappearEase = Ease.InOutQuad;
    private Image cardSprite;
    private CanvasGroup canvasGroup;
    private RectTransform rt;
    private float originalWidth;

    [SerializeField] private GameObject cardPrefab;
    public GameObject CardPreFab { get { return cardPrefab; } set { cardPrefab = value; } }

    private CardData cardData;
    private UICardDescPanel panel;
    private PlayerState playerState;

    private void Awake()
    {
        cardSprite = GetComponentInChildren<Image>();
        cardData = GetComponent<CardData>();
        panel = FindObjectOfType<UICardDescPanel>();
        playerState = FindObjectOfType<PlayerState>();
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

    public void CardAnimation()
    {
        DOTween.Sequence()
            .Append(rt.DOSizeDelta(new Vector2(originalWidth, rt.sizeDelta.y), fadeDuration).SetEase(enableEase))
            .Join(canvasGroup.DOFade(1f, fadeDuration).SetEase(enableEase))
            .Join(cardSprite.rectTransform.DOAnchorPosY(0f, duration).SetEase(enableEase));

        if (cardPrefab != null)
            playerState?.AddCard(cardPrefab);
    }
}
