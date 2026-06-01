using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class PauseMenuSlideAnim : MonoBehaviour, IUIAnimation
{
    public float Duration
    {
        get => duration;
        set => duration = value;
    }

    [SerializeField] private RectTransform target;
    [SerializeField] private float duration = 0.4f;
    [SerializeField] private float startOffsetX = 600f;
    [SerializeField] private Ease startAnimEase = Ease.OutCubic;
    [SerializeField] private Ease endAnimEase = Ease.InCubic;
    [SerializeField] private UnityEvent OnAnimationStart;
    [SerializeField] private UnityEvent OnAnimationEnd;

    public void StartAnimation(float delay)
    {
        if (target == null) return;
        target.DOKill(true);
        target.anchoredPosition = new Vector2(startOffsetX, target.anchoredPosition.y);
        target.DOAnchorPosX(0f, duration)
            .SetEase(startAnimEase)
            .SetDelay(delay)
            .OnStart(() => OnAnimationStart?.Invoke());
    }

    public void EndAnimation()
    {
        if (target == null) return;
        target.DOKill(true);
        target.DOAnchorPosX(startOffsetX, duration)
            .SetEase(endAnimEase)
            .OnComplete(() => OnAnimationEnd?.Invoke());
    }

    private void OnDestroy()
    {
        target?.DOKill();
    }
}
