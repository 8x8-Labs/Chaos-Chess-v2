using DG.Tweening;
using UnityEngine;

public abstract class SceneLoadingOverlayBase : MonoBehaviour
{
    public abstract float Alpha { get; }

    public abstract void Initialize();
    public abstract Tween FadeTo(float targetAlpha, float duration);

    public virtual void SetAlpha(float alpha) { }
    public virtual void SetProgress(float progress) { }
    public virtual void SetLoadingContentVisible(bool isVisible) { }
}
