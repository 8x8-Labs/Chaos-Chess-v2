using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SceneLoadingOverlayRain : SceneLoadingOverlayBase
{
    [SerializeField] private Graphic transitionGraphic;
    [SerializeField] private GameObject loadingContent;
    [SerializeField] private Slider progressSlider;

    private static readonly int ValueProperty = Shader.PropertyToID("_Value");
    private Material materialInstance;
    private Tween valueTween;

    public override float Alpha => materialInstance != null ? materialInstance.GetFloat(ValueProperty) : 0f;

    private void Awake()
    {
        if (transitionGraphic == null)
            return;

        materialInstance = new Material(transitionGraphic.material);
        transitionGraphic.material = materialInstance;
    }

    private void OnDestroy()
    {
        valueTween?.Kill();
        if (materialInstance != null)
            Destroy(materialInstance);
    }

    public override void Initialize()
    {
        valueTween?.Kill();
        SetProgress(0f);
        SetLoadingContentVisible(false);
        SetShaderValue(0f);
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

    public override Tween FadeTo(float targetAlpha, float duration)
    {
        if (materialInstance == null)
            return null;

        valueTween?.Kill();

        if (transitionGraphic != null)
            transitionGraphic.raycastTarget = true;

        if (duration <= 0f)
        {
            SetShaderValue(targetAlpha);
            if (transitionGraphic != null)
                transitionGraphic.raycastTarget = targetAlpha > 0f;
            return null;
        }

        valueTween = DOVirtual.Float(Alpha, targetAlpha, duration, SetShaderValue)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (transitionGraphic != null)
                    transitionGraphic.raycastTarget = targetAlpha > 0f;
            });

        return valueTween;
    }

    public override void SetAlpha(float alpha) => SetShaderValue(alpha);

    private void SetShaderValue(float value)
    {
        if (materialInstance != null)
            materialInstance.SetFloat(ValueProperty, value);
    }
}
