using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class GlobalCardUI : MonoBehaviour
{
    public Image CardImage;
    public TMP_Text Title;
    public TMP_Text RemainTurn;

    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private float yOffset = 160f;

    public float AnimationDuration => animationDuration;

    public void PlayAppearAnimation(Action onComplete = null)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchoredPosition -= new Vector2(0f, yOffset);
        rt.DOAnchorPosY(yOffset, animationDuration).SetRelative().OnComplete(() => onComplete?.Invoke());
    }

    public void PlayDisappearAnimation(Action onComplete)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.DOAnchorPosY(rt.anchoredPosition.y - yOffset, animationDuration)
          .SetEase(Ease.InQuad)
          .OnComplete(() => onComplete?.Invoke());
    }
}
