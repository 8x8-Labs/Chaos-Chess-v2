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
    [SerializeField] private float destroyDuration = 0.25f;

    private readonly Dictionary<GlobalEffector, GlobalCardData> _displayedCards = new();
    private readonly List<GlobalCardUI> _displayedCardUI = new();

    private void OnEnable()
    {
        GlobalEffector.OnActivated += HandleActivated;
        GlobalEffector.OnDeactivated += HandleDeactivated;
        GlobalEffector.OnTurnTicked += HandleTurnTicked;
    }

    private void OnDisable()
    {
        GlobalEffector.OnActivated -= HandleActivated;
        GlobalEffector.OnDeactivated -= HandleDeactivated;
        GlobalEffector.OnTurnTicked -= HandleTurnTicked;
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


    private void HandleDeactivated(GlobalEffector ge)
    {
        if (!_displayedCards.TryGetValue(ge, out var rt)) return;

        _displayedCards.Remove(ge);
        layout.RemoveCard(rt.Rt);

        var cg = rt.Rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.Rt.gameObject.AddComponent<CanvasGroup>();

        cg.DOFade(0f, destroyDuration).SetEase(Ease.InQuad);
        rt.CardUI.PlayDisappearAnimation(() => Destroy(rt.Rt.gameObject));
    }
}
