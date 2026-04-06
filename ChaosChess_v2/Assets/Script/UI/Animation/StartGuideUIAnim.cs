using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StartGuideUIAnim : MonoBehaviour
{
    [SerializeField] private List<Sprite> sprites;
    [SerializeField] private List<Image> pieces;
    [SerializeField] private float floatAmount = 15f;
    [SerializeField] private float floatTime = 1.2f;
    [SerializeField] private float rotateAngle = 8f;
    [SerializeField] private float rotateTime = 1.6f;
    [SerializeField] private float randomRotRange = 15f;

    private List<Tween> tweens = new List<Tween>();
    private bool isPlaying = false;

    private void Start()
    {
        Play();
    }

    public void Play()
    {
        isPlaying = true;
        ApplyRandomSprites();   // ✅ isPlaying 체크 전에 항상 스프라이트 교체

        // 기존 트윈 정리 후 재생성
        foreach (var t in tweens) t.Kill();
        tweens.Clear();
        CreateTweens();
    }

    public void Stop()
    {
        if (!isPlaying) return;
        isPlaying = false;
        foreach (var t in tweens) t.Pause();
    }

    private void ApplyRandomSprites()
    {
        if (sprites == null || sprites.Count == 0) return;

        List<Sprite> pool = new List<Sprite>(sprites);
        foreach (Image piece in pieces)
        {
            if (pool.Count == 0) pool = new List<Sprite>(sprites);
            int idx = Random.Range(0, pool.Count);
            piece.sprite = pool[idx];
            pool.RemoveAt(idx);

            float randomRot = Random.Range(-randomRotRange, randomRotRange);
            piece.rectTransform.localEulerAngles = new Vector3(0f, 0f, randomRot);
        }
    }

    private void CreateTweens()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            RectTransform rt = pieces[i].rectTransform;
            float originY = rt.anchoredPosition.y;
            float originRot = rt.localEulerAngles.z;

            float floatDelay = i * (floatTime / pieces.Count);
            float rotDelay = i * (rotateTime / pieces.Count);

            // ✅ SetRelative 제거, 절대값 기준으로 Yoyo
            tweens.Add(
                rt.DOAnchorPosY(originY + floatAmount, floatTime)
                    .SetEase(Ease.InOutSine)
                    .SetDelay(floatDelay)
                    .SetLoops(-1, LoopType.Yoyo)
            );

            // ✅ DOLocalRotate SetRelative 제거 → 각도 누적 버그 방지
            tweens.Add(
                DOTween.To(
                    () => rt.localEulerAngles.z,
                    angle => rt.localEulerAngles = new Vector3(0f, 0f, angle),
                    originRot + rotateAngle,
                    rotateTime
                )
                .SetEase(Ease.InOutSine)
                .SetDelay(rotDelay)
                .SetLoops(-1, LoopType.Yoyo)
            );
        }
    }

    private void OnDestroy()
    {
        foreach (var t in tweens) t.Kill();
        tweens.Clear();
    }
}