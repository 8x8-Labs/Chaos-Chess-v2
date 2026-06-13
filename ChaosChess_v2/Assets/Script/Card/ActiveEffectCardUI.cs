using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ActiveEffectCardUI : MonoBehaviour
{
    public Image CardImage;
    public TMP_Text Title;
    public TMP_Text RemainTurn;

    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private float yOffset = 160f;

    private RectTransform cardRect => CardImage != null ? CardImage.rectTransform : null;

    public float AnimationDuration => animationDuration;

    public void PlayAppearAnimation(Action onComplete = null)
    {
        if(cardRect == null)
        {
            onComplete?.Invoke();
            return;
        }
        cardRect.anchoredPosition = new Vector2(0f, -yOffset);
        cardRect.DOAnchorPosY(0f, animationDuration).OnComplete(() => onComplete?.Invoke());
    }

    public void PlayDisappearAnimation(Action onComplete)
    {
        if (cardRect == null)
        {
            onComplete?.Invoke();
            return;
        }
        cardRect.DOAnchorPosY(-yOffset, animationDuration)
          .SetEase(Ease.InQuad)
          .OnComplete(() => onComplete?.Invoke());
    }
}
