using DG.Tweening;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UI;

public class ModeUIAnim : MonoBehaviour
{
    [SerializeField] private Image rootImage;
    [SerializeField] private List<Image> firstImages;
    [SerializeField] private List<Image> secondImages;

    [Header("Float Animation")]
    [SerializeField] private float rootMoveAmount = 20f;
    [SerializeField] private float rootDuration    = 1.5f;
    [SerializeField] private float childMoveAmount = 12f;
    [SerializeField] private float childDuration   = 2.5f;

    private List<Tween> tweens = new List<Tween>();
    private bool isPlaying = false;

    private void Start()
    {
        Play();
    }

    public void Play()
    {
        isPlaying = true;

        // 기존 트윈 정리 후 재생성
        foreach (var t in tweens) t.Kill();
        tweens.Clear();
        CreateTweens();
    }

    private void CreateTweens()
    {
        // 상하 부유 트윈
        if (rootImage != null)
        {
            RectTransform rt = rootImage.rectTransform;
            float originY = rt.anchoredPosition.y;
            tweens.Add(
                rt.DOAnchorPosY(originY + rootMoveAmount, rootDuration)
                  .SetEase(Ease.InOutSine)
                  .SetLoops(-1, LoopType.Yoyo)
            );
        }

        foreach (var img in firstImages)
        {
            if (img == null) continue;
            RectTransform rt = img.rectTransform;
            float originY = rt.anchoredPosition.y;
            tweens.Add(
                rt.DOAnchorPosY(originY + childMoveAmount, childDuration)
                  .SetEase(Ease.InOutSine)
                  .SetLoops(-1, LoopType.Yoyo)
            );
        }

        foreach (var img in secondImages)
        {
            if (img == null) continue;
            RectTransform rt = img.rectTransform;
            float originY = rt.anchoredPosition.y;
            tweens.Add(
                rt.DOAnchorPosY(originY + childMoveAmount, childDuration)
                  .SetEase(Ease.InOutSine)
                  .SetLoops(-1, LoopType.Yoyo)
            );
        }
    }
}
