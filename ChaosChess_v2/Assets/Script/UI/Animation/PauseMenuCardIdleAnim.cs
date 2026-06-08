using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PauseMenuCardIdleAnim : MonoBehaviour
{
    [SerializeField] private List<Image> cards;

    [Header("Float")]
    [SerializeField] private float floatAmount = 12f;
    [SerializeField] private float floatDuration = 1.3f;

    [Header("Rotation (Z)")]
    [SerializeField] private float rotateAngle = 10f;
    [SerializeField] private float rotateDuration = 1.8f;
    [SerializeField] private float randomRotRange = 20f;

    [Header("3D Tilt (Y)")]
    [SerializeField] private float tiltAngleY = 15f;
    [SerializeField] private float tiltDurationY = 2.2f;
    [SerializeField] private float randomTiltYRange = 10f;

    [Header("3D Tilt (X)")]
    [SerializeField] private float tiltAngleX = 5f;
    [SerializeField] private float tiltDurationX = 2.8f;

    private List<Tween> tweens = new List<Tween>();
    private List<Sprite> cardSprites;

    private void Start()
    {
        cardSprites = GetRandomCardSprites(3);
        Play();
    }

    private List<Sprite> GetRandomCardSprites(int count)
    {
        var result = new List<Sprite>();

        if (PlayerState.Instance == null || PlayerState.Instance.CardPool == null) return result;

        var pool = new List<GameObject>(PlayerState.Instance.CardPool);

        while (result.Count < count && pool.Count > 0)
        {
            int idx = Random.Range(0, pool.Count);
            GameObject cardObj = pool[idx];
            pool.RemoveAt(idx);

            CardData cardData = cardObj != null ? cardObj.GetComponent<CardData>() : null;
            Sprite sprite = cardData?.DataSO?.CardImage;
            if (sprite != null)
                result.Add(sprite);
        }

        return result;
    }

    public void Play()
    {
        foreach (var t in tweens) t.Kill();
        tweens.Clear();

        if (cards == null || cards.Count == 0) return;

        ApplyRandomSprites();
        CreateTweens();
    }

    public void Stop()
    {
        foreach (var t in tweens) t.Pause();
    }

    private void ApplyRandomSprites()
    {
        if (cardSprites == null || cardSprites.Count == 0) return;

        List<Sprite> pool = new List<Sprite>(cardSprites);
        foreach (Image card in cards)
        {
            if (card == null) continue;
            if (pool.Count == 0) pool = new List<Sprite>(cardSprites);

            int idx = Random.Range(0, pool.Count);
            card.sprite = pool[idx];
            pool.RemoveAt(idx);

            float randomZ = Random.Range(-randomRotRange, randomRotRange);
            float randomY = Random.Range(-randomTiltYRange, randomTiltYRange);
            card.rectTransform.localEulerAngles = new Vector3(0f, randomY, randomZ);
        }
    }

    private void CreateTweens()
    {
        int count = cards.Count;
        for (int i = 0; i < count; i++)
        {
            if (cards[i] == null) continue;

            RectTransform rt = cards[i].rectTransform;
            float originPosY = rt.anchoredPosition.y;
            Vector3 originEuler = rt.localEulerAngles;

            // 각 카드마다 위상 오프셋 (세 축 모두 다른 스태거)
            float floatDelay = i * (floatDuration / count);
            float rotZDelay  = i * (rotateDuration / count);
            float rotYDelay  = i * (tiltDurationY / count);
            float rotXDelay  = i * (tiltDurationX / count);

            // 세 축 회전값을 공유 캡처 변수로 관리 → 동시 수정 충돌 방지
            float rotX = originEuler.x;
            float rotY = originEuler.y;
            float rotZ = originEuler.z;

            tweens.Add(
                rt.DOAnchorPosY(originPosY + floatAmount, floatDuration)
                    .SetEase(Ease.InOutSine)
                    .SetDelay(floatDelay)
                    .SetLoops(-1, LoopType.Yoyo)
            );

            // Z축 회전 (기존 기울기)
            tweens.Add(
                DOTween.To(
                    () => rotZ,
                    v => { rotZ = v; rt.localEulerAngles = new Vector3(rotX, rotY, rotZ); },
                    rotZ + rotateAngle,
                    rotateDuration
                )
                .SetEase(Ease.InOutSine)
                .SetDelay(rotZDelay)
                .SetLoops(-1, LoopType.Yoyo)
            );

            // Y축 회전 (3D 원근감 — 카드가 좌우로 살짝 돌아가는 느낌)
            tweens.Add(
                DOTween.To(
                    () => rotY,
                    v => { rotY = v; rt.localEulerAngles = new Vector3(rotX, rotY, rotZ); },
                    rotY + tiltAngleY,
                    tiltDurationY
                )
                .SetEase(Ease.InOutSine)
                .SetDelay(rotYDelay)
                .SetLoops(-1, LoopType.Yoyo)
            );

            // X축 회전 (미세한 앞뒤 기울기로 입체감 강조)
            tweens.Add(
                DOTween.To(
                    () => rotX,
                    v => { rotX = v; rt.localEulerAngles = new Vector3(rotX, rotY, rotZ); },
                    rotX + tiltAngleX,
                    tiltDurationX
                )
                .SetEase(Ease.InOutSine)
                .SetDelay(rotXDelay)
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
