using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SceneLoadingOverlayUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private GameObject loadingContent;
    [SerializeField] private Slider progressSlider;

    private Tween fadeTween;

    public float Alpha => overlayGroup != null ? overlayGroup.alpha : 0f;

    public void Initialize()
    {
        fadeTween?.Kill();
        SetProgress(0f);
        SetLoadingContentVisible(false);
        SetAlpha(0f);
        SetBlocking(false);
    }

    public Tween FadeTo(float targetAlpha, float duration)
    {
        if (overlayGroup == null)
            return null;

        fadeTween?.Kill();

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            SetBlocking(targetAlpha > 0f);
            return null;
        }

        SetBlocking(true);

        fadeTween = overlayGroup
            .DOFade(targetAlpha, duration)
            .SetUpdate(true)
            .OnComplete(() => SetBlocking(targetAlpha > 0f));

        return fadeTween;
    }

    public void SetAlpha(float alpha)
    {
        if (overlayGroup == null)
            return;

        overlayGroup.alpha = alpha;
    }

    public void SetBlocking(bool isBlocking)
    {
        if (overlayGroup == null)
            return;

        overlayGroup.blocksRaycasts = isBlocking;
        overlayGroup.interactable = isBlocking;
    }

    public void SetLoadingContentVisible(bool isVisible)
    {
        if (loadingContent != null)
            loadingContent.SetActive(isVisible);
    }

    public void SetProgress(float progress)
    {
        if (progressSlider != null)
            progressSlider.value = Mathf.Clamp01(progress);
    }
}
