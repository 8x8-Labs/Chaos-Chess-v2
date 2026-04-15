using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BasicUIAnimation : MonoBehaviour, IUIAnimation
{
    public float Duration
    {
        get
        {
            return duration;
        }
        set
        {
            duration = value;
        }
    }

    [SerializeField] private float duration;
    [SerializeField] private UnityEvent OnAnimationStart;
    [SerializeField] private UnityEvent OnAnimationEnd;
    [SerializeField] private Ease startAnimEase;
    [SerializeField] private Ease endAnimEase;

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    public void EndAnimation()
    {
        image.rectTransform.DOMoveX(0f, duration).SetEase(endAnimEase)
            .OnComplete(() => OnAnimationEnd?.Invoke());
    }

    public void StartAnimation(float delay)
    {
        image.DOKill(true);
        image.rectTransform.anchoredPosition = new Vector2(2000f, 0f);
        image.rectTransform.DOMoveX(0f, duration)
            .SetEase(startAnimEase)
            .SetDelay(delay)
            .OnStart(() => OnAnimationStart?.Invoke());
    }
}
