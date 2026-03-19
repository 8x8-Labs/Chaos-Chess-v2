using DG.Tweening;
using UnityEngine;
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
    [SerializeField] private Ease startAnimEase;
    [SerializeField] private Ease endAnimEase;

    private Image image;

    private void Start()
    {
        image = GetComponent<Image>();
    }

    public void EndAnimation()
    {
        image.rectTransform.DOMoveX(0f, duration).SetEase(endAnimEase);
    }

    public void StartAnimation()
    {
        image.rectTransform.anchoredPosition = new Vector2(-500f, 0f);
        image.rectTransform.DOMoveX(0f, duration).SetEase(startAnimEase);
    }
}
