using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CardHandLayout : MonoBehaviour
{
    [SerializeField] public RectTransform areaBounds;
    [SerializeField] private float overlap = 40f;
    [SerializeField] private float cardY = 0f;
    [SerializeField] private float rearrangeDuration = 0.3f;
    [SerializeField] private GameObject inputBlockPanel;

    private readonly List<RectTransform> _cards = new();
    private GameManager gameManager;
    private bool isSubscribed;

    private void OnEnable()
    {
        SubscribeGameManager();
    }

    private void Start()
    {
        SubscribeGameManager();
    }

    private void OnDisable()
    {
        if (isSubscribed && gameManager != null)
        {
            gameManager.OnPlayerCheckStateChanged -= UpdateTurnInputBlocked;
            gameManager.OnHalfTurnChanged -= UpdateTurnInputBlocked;
        }

        isSubscribed = false;
    }

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

    public void SetArenaInputBlocked(bool isBlocked)
    {
        SetInputBlocked(isBlocked);

        if (!isBlocked)
            UpdateTurnInputBlocked();
    }

    private void SubscribeGameManager()
    {
        if (isSubscribed)
            return;

        gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        gameManager.OnPlayerCheckStateChanged += UpdateTurnInputBlocked;
        gameManager.OnHalfTurnChanged += UpdateTurnInputBlocked;
        isSubscribed = true;
        UpdateTurnInputBlocked();
    }

    private void UpdateTurnInputBlocked(bool _) => UpdateTurnInputBlocked();

    private void UpdateTurnInputBlocked()
    {
        if (gameManager.IsArenaMode)
            return;

        SetInputBlocked(gameManager.IsPlayerInCheck || !gameManager.IsPlayerTurn);
    }

    private void SetInputBlocked(bool isBlocked)
    {
        if (inputBlockPanel == null)
            return;

        inputBlockPanel.SetActive(isBlocked);

        if (isBlocked)
            inputBlockPanel.transform.SetAsLastSibling();
    }

    private void Refresh(bool animate)
    {
        int n = _cards.Count;
        if (n == 0) return;

        float cardWidth = _cards[0].sizeDelta.x;
        float totalWidth = cardWidth + (n - 1) * overlap;
        float startX = areaBounds.rect.center.x - totalWidth * 0.5f + cardWidth * 0.5f;

        for (int i = 0; i < n; i++)
        {
            if (_cards[i] == null) continue;

            float x = startX + i * overlap;
            _cards[i].DOKill();
            if (animate)
                _cards[i].DOAnchorPos(new Vector2(x, cardY), rearrangeDuration).SetEase(Ease.OutQuad);
            else
                _cards[i].anchoredPosition = new Vector2(x, cardY);
        }

        UpdateSiblingIndices();
    }

    // 왼쪽 카드(index 0)가 위에 오도록 역순으로 SetAsLastSibling
    private void UpdateSiblingIndices()
    {
        for (int i = _cards.Count - 1; i >= 0; i--)
            _cards[i].SetAsLastSibling();

        if (inputBlockPanel != null && inputBlockPanel.activeSelf)
            inputBlockPanel.transform.SetAsLastSibling();
    }
}
