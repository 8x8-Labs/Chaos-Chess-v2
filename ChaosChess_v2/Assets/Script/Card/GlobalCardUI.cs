using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalCardUI : MonoBehaviour
{
    public Image CardImage;
    public TMP_Text Title;
    public TMP_Text RemainTurn;

    public void PlayAppearAnimation(Action onComplete = null)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchoredPosition -= new Vector2(0f, 160f);
        rt.DOAnchorPosY(160f, 0.2f).SetRelative().OnComplete(() => onComplete?.Invoke());
    }

    public void PlayDisappearAnimation(Action onComplete)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.DOAnchorPosY(rt.anchoredPosition.y - 160f, 0.2f)
          .SetEase(Ease.InQuad)
          .OnComplete(() => onComplete?.Invoke());
    }
}
