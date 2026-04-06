using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class StartGameUIAnim : MonoBehaviour
{
    [SerializeField] private List<RectTransform> backgrounds;
    [SerializeField] private RectTransform frontPiece;
    [SerializeField] private RectTransform backPiece;
    [SerializeField] private float wiggleValue = 20f;
    [SerializeField] private float wiggleTime = 1f;
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
        for (int i = 0; i < backgrounds.Count; i++)
        {
            RectTransform bg = backgrounds[i];
            float duration = wiggleTime + (i * 0.2f);

            tweens.Add(
                bg.DOAnchorPosY(wiggleValue, duration)
                    .SetRelative(true)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
            );
        }

        tweens.Add(
            frontPiece.DOAnchorPosY(wiggleValue * 0.5f, pieceWiggleTime)
                .SetRelative(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
        );

        tweens.Add(
            backPiece.DOShakeAnchorPos(2f, 5f, 30, 90, false, false)
                .SetLoops(-1, LoopType.Restart)
        );
    }

    private void OnDestroy()
    {
        foreach (var t in tweens) t.Kill();
        tweens.Clear();
    }
}
