using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class StartPracticeUIAnim : MonoBehaviour
{
    [SerializeField] private RectTransform frontPiece;
    [SerializeField] private RectTransform backPiece;
    [SerializeField] private float wiggleValue = 20f;
    [SerializeField] private float pieceWiggleTime = 1f;

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
        tweens.Add(
            frontPiece.DOAnchorPosY(wiggleValue * 0.5f, pieceWiggleTime)
                .SetRelative(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
        );

        tweens.Add(
            backPiece.DOAnchorPosY(wiggleValue * 0.5f, pieceWiggleTime)
                .SetRelative(true)
                .SetEase(Ease.InOutSine)
                .SetDelay(pieceWiggleTime * 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
        );
    }

    private void OnDestroy()
    {
        foreach (var t in tweens) t.Kill();
        tweens.Clear();
    }
}
