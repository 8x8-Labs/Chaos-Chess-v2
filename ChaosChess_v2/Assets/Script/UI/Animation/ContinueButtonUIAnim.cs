using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class ContinueButtonUIAnim : MonoBehaviour
{
    [SerializeField] private List<RectTransform> floatingObjects;
    [SerializeField] private RectTransform centerPiece;

    [Header("Float")]
    [SerializeField] private float floatAmount = 10f;
    [SerializeField] private float floatDuration = 1.4f;

    [Header("Rotation")]
    [SerializeField] private float rotateAngle = 6f;
    [SerializeField] private float rotateDuration = 2.0f;

    [Header("Center Piece")]
    [SerializeField] private float pieceFloatAmount = 14f;
    [SerializeField] private float pieceFloatDuration = 1.2f;

    private List<Tween> tweens = new List<Tween>();
    private bool isPlaying = false;

    private void Start()
    {
        Play();
    }

    public void Play()
    {
        if (isPlaying) return;
        isPlaying = true;

        if (tweens.Count == 0)
            CreateTweens();
        else
            foreach (var t in tweens) t.Play();
    }

    public void Stop()
    {
        if (!isPlaying) return;
        isPlaying = false;

        foreach (var t in tweens) t.Pause();
    }

    private void CreateTweens()
    {
        for (int i = 0; i < floatingObjects.Count; i++)
            AddFloatAndRotate(floatingObjects[i], delay: i * floatDuration * 0.3f);

        if (centerPiece != null)
        {
            tweens.Add(
                centerPiece.DOAnchorPosY(pieceFloatAmount, pieceFloatDuration)
                    .SetRelative(true)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
            );
        }
    }

    private void AddFloatAndRotate(RectTransform rt, float delay)
    {
        if (rt == null) return;

        tweens.Add(
            rt.DOAnchorPosY(floatAmount, floatDuration)
                .SetRelative(true)
                .SetEase(Ease.InOutSine)
                .SetDelay(delay)
                .SetLoops(-1, LoopType.Yoyo)
        );

        tweens.Add(
            rt.DOLocalRotate(new Vector3(0f, 0f, rotateAngle), rotateDuration)
                .SetRelative(true)
                .SetEase(Ease.InOutSine)
                .SetDelay(delay)
                .SetLoops(-1, LoopType.Yoyo)
        );
    }

    private void OnDestroy()
    {
        foreach (var t in tweens) t.Kill();
        tweens.Clear();
    }
}
