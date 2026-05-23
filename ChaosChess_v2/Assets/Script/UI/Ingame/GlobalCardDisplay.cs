using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public struct GlobalCardData
{
    public RectTransform Rt;
    public GlobalCardUI CardUI;
}

public class GlobalCardDisplay : MonoBehaviour
{
    [SerializeField] private CardHandLayout layout;
    [SerializeField] private GameObject cardPrefab;

    private readonly Dictionary<GlobalEffector, GlobalCardData> _displayedCards = new();
    private GlobalCardData _arenaCard;
    private bool _isArenaCardDisplayed;
    private ArenaManager _subscribedArenaManager;

    private void OnEnable()
    {
        GlobalEffector.OnActivated += HandleActivated;
        GlobalEffector.OnDeactivated += HandleDeactivated;
        GlobalEffector.OnTurnTicked += HandleTurnTicked;
        TrySubscribeArenaManager();
    }

    private void Start()
    {
        TrySubscribeArenaManager();
    }

    private void OnDisable()
    {
        GlobalEffector.OnActivated -= HandleActivated;
        GlobalEffector.OnDeactivated -= HandleDeactivated;
        GlobalEffector.OnTurnTicked -= HandleTurnTicked;
        UnsubscribeArenaManager();
    }

    private void TrySubscribeArenaManager()
    {
        if (_subscribedArenaManager != null) return;

        ArenaManager arenaManager = ArenaManager.Instance;
        if (arenaManager == null) return;

        _subscribedArenaManager = arenaManager;
        _subscribedArenaManager.ArenaStarted += HandleArenaStarted;
        _subscribedArenaManager.ArenaRemainingTurnsChanged += HandleArenaRemainingTurnsChanged;
        _subscribedArenaManager.ArenaEnded += HandleArenaEnded;
    }

    private void UnsubscribeArenaManager()
    {
        if (_subscribedArenaManager == null) return;

        _subscribedArenaManager.ArenaStarted -= HandleArenaStarted;
        _subscribedArenaManager.ArenaRemainingTurnsChanged -= HandleArenaRemainingTurnsChanged;
        _subscribedArenaManager.ArenaEnded -= HandleArenaEnded;
        _subscribedArenaManager = null;
    }

    private void HandleActivated(GlobalEffector ge)
    {
        if (ge.CardSO == null || !ge.CardSO.ShowStatusCard) return;

        var instance = Instantiate(cardPrefab, layout.areaBounds);

        GlobalCardData data = new();

        GlobalCardUI cardUI = instance.GetComponent<GlobalCardUI>();
        RectTransform rect = instance.GetComponent<RectTransform>();

        cardUI.CardImage.sprite = ge.CardSO.CardImage;
        cardUI.RemainTurn.text = GetDisplayText(ge);
        cardUI.Title.text = ge.IsPermanent ? ge.CardSO.CardName : "남은 턴";

        data.Rt = rect;
        data.CardUI = cardUI;

        _displayedCards[ge] = data;
        layout.AddCard(data.Rt);
        cardUI.PlayAppearAnimation(() => layout.RefreshAnimated());
    }

    private void HandleTurnTicked(GlobalEffector ge)
    {
        if (!_displayedCards.TryGetValue(ge, out var rt)) return;
        rt.CardUI.RemainTurn.text = GetDisplayText(ge);
    }

    private static string GetDisplayText(GlobalEffector ge) =>
        ge.IsPermanent ? "" : $"{ge.RemainingTurns}";

    private void HandleArenaStarted(CardDataSO cardSO, int remainingTurns)
    {
        if (_isArenaCardDisplayed)
            RemoveArenaCard();

        var instance = Instantiate(cardPrefab, layout.areaBounds);

        GlobalCardUI cardUI = instance.GetComponent<GlobalCardUI>();
        RectTransform rect = instance.GetComponent<RectTransform>();

        if (cardSO != null)
            cardUI.CardImage.sprite = cardSO.CardImage;

        cardUI.RemainTurn.text = $"{remainingTurns}";
        cardUI.Title.text = "남은 턴";

        _arenaCard = new GlobalCardData
        {
            Rt = rect,
            CardUI = cardUI
        };
        _isArenaCardDisplayed = true;

        layout.AddCard(_arenaCard.Rt);
        cardUI.PlayAppearAnimation(() => layout.RefreshAnimated());
    }

    private void HandleArenaRemainingTurnsChanged(int remainingTurns)
    {
        if (!_isArenaCardDisplayed) return;

        _arenaCard.CardUI.RemainTurn.text = $"{remainingTurns}";
    }

    private void HandleArenaEnded()
    {
        if (!_isArenaCardDisplayed) return;

        RemoveArenaCard();
    }

    private void RemoveArenaCard()
    {
        GlobalCardData arenaCard = _arenaCard;
        _isArenaCardDisplayed = false;
        layout.RemoveCard(arenaCard.Rt);

        var cg = arenaCard.Rt.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.DOFade(0f, arenaCard.CardUI.AnimationDuration).SetEase(Ease.InQuad);
        arenaCard.CardUI.PlayDisappearAnimation(() => Destroy(arenaCard.Rt.gameObject));
    }

    private void HandleDeactivated(GlobalEffector ge)
    {
        if (!_displayedCards.TryGetValue(ge, out var rt)) return;

        _displayedCards.Remove(ge);
        layout.RemoveCard(rt.Rt);

        var cg = rt.Rt.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.DOFade(0f, rt.CardUI.AnimationDuration).SetEase(Ease.InQuad);
        rt.CardUI.PlayDisappearAnimation(() => Destroy(rt.Rt.gameObject));
    }
}
