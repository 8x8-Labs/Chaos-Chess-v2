using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CardHandLayout : MonoBehaviour
{
    [SerializeField] public RectTransform areaBounds;
    [SerializeField] private float overlap = 40f;
    [SerializeField] private float cardY = 0f;
    [SerializeField] private float rearrangeDuration = 0.3f;

    private readonly List<RectTransform> _cards = new();
    private readonly List<Tweener> _activeTweens = new();

    public void AddCard(RectTransform card)
    {
        _cards.Add(card);
        Refresh(animate: false);
    }

    public void RemoveCard(RectTransform card)
    {
        _cards.Remove(card);
        Refresh(animate: true);
    }

    public void RefreshAnimated() => Refresh(animate: true);

    private void Refresh(bool animate)
    {
        int n = _cards.Count;
        if (n == 0) return;

        float cardWidth = _cards[0].sizeDelta.x;
        float totalWidth = cardWidth + (n - 1) * overlap;
        float startX = areaBounds.rect.center.x - totalWidth * 0.5f + cardWidth * 0.5f;

        KillActiveTweens();
        for (int i = 0; i < n; i++)
        {
            float x = startX + i * overlap;
            if (animate)
            {
                var tween = _cards[i].DOAnchorPos(new Vector2(x, cardY), rearrangeDuration).SetEase(Ease.OutQuad);
                _activeTweens.Add(tween);
            }
            else
            {
                _cards[i].anchoredPosition = new Vector2(x, cardY);
            }
        }

        UpdateSiblingIndices();
    }

    // 왼쪽 카드(index 0)가 위에 오도록 역순으로 SetAsLastSibling
    private void UpdateSiblingIndices()
    {
        for (int i = _cards.Count - 1; i >= 0; i--)
            _cards[i].SetAsLastSibling();
    }

    private void KillActiveTweens()
    {
        foreach (var tween in _activeTweens)
            if (tween.IsActive()) tween.Kill();
        _activeTweens.Clear();
    }
}
