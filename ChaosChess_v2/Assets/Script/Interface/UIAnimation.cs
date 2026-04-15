using DG.Tweening;
using UnityEngine;

public interface IUIAnimation
{
    public float Duration { get; set; }
    public void StartAnimation(float delay);
    public void EndAnimation();
}
