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

    private RectTransform cardRect => CardImage?.rectTransform;

    public float AnimationDuration => animationDuration;

    public void PlayAppearAnimation(Action onComplete = null)
    {
        cardRect.anchoredPosition = new Vector2(0f, -yOffset);
        cardRect.DOAnchorPosY(0f, animationDuration).OnComplete(() => onComplete?.Invoke());
    }

    public void PlayDisappearAnimation(Action onComplete)
    {
        cardRect.DOAnchorPosY(-yOffset, animationDuration)
          .SetEase(Ease.InQuad)
          .OnComplete(() => onComplete?.Invoke());
    }
}
