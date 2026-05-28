using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SceneLoadingOverlayUI : SceneLoadingOverlayBase
{
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private GameObject loadingContent;
    [SerializeField] private Slider progressSlider;

    private Tween fadeTween;

    public override float Alpha => overlayGroup != null ? overlayGroup.alpha : 0f;

    public override void Initialize()
    {
        fadeTween?.Kill();
        SetProgress(0f);
        SetLoadingContentVisible(false);
        SetAlpha(0f);
        SetBlocking(false);
    }

    public override Tween FadeTo(float targetAlpha, float duration)
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

    public override void SetAlpha(float alpha)
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

    public override void SetLoadingContentVisible(bool isVisible)
    {
        if (loadingContent != null)
            loadingContent.SetActive(isVisible);
    }

    public override void SetProgress(float progress)
    {
        if (progressSlider != null)
            progressSlider.value = Mathf.Clamp01(progress);
    }
}
