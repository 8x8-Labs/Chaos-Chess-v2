using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class IconButtonIdleAnim : MonoBehaviour
{
    [SerializeField] private Image iconImage;

    [Header("Rotation")]
    [SerializeField] private float rotateAngle = 8f;
    [SerializeField] private float rotateDuration = 2.0f;

    [Header("Float")]
    [SerializeField] private bool useFloat = true;
    [SerializeField] private float floatAmount = 8f;
    [SerializeField] private float floatDuration = 1.6f;

    [Header("Phase")]
    [SerializeField] private float startDelay = 0f;

    private List<Tween> tweens = new List<Tween>();

    private void Start()
    {
        if (iconImage == null)
            iconImage = GetComponent<Image>();

        Play();
    }

    public void Play()
    {
        foreach (var t in tweens) t.Kill();
        tweens.Clear();

        if (iconImage == null) return;

        RectTransform rt = iconImage.rectTransform;
        float originRot = rt.localEulerAngles.z;

        tweens.Add(
            DOTween.To(
                () => rt.localEulerAngles.z,
                angle => rt.localEulerAngles = new Vector3(0f, 0f, angle),
                originRot + rotateAngle,
                rotateDuration
            )
            .SetEase(Ease.InOutSine)
            .SetDelay(startDelay)
            .SetLoops(-1, LoopType.Yoyo)
        );

        if (useFloat)
        {
            float originY = rt.anchoredPosition.y;
            tweens.Add(
                rt.DOAnchorPosY(originY + floatAmount, floatDuration)
                    .SetEase(Ease.InOutSine)
                    .SetDelay(startDelay)
                    .SetLoops(-1, LoopType.Yoyo)
            );
        }
    }

    public void Stop()
    {
        foreach (var t in tweens) t.Pause();
    }

    private void OnDestroy()
    {
        foreach (var t in tweens) t.Kill();
        tweens.Clear();
    }
}
