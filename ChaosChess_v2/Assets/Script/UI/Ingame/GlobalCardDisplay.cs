using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalCardDisplay : MonoBehaviour
{
    [SerializeField] private CardHandLayout layout;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float destroyDuration = 0.25f;

    private readonly Dictionary<GlobalEffector, RectTransform> _displayedCards = new();

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
        if (ge.CardSO == null) return;

        var instance = Instantiate(cardPrefab, layout.areaBounds);
        var rt = instance.GetComponent<RectTransform>();

        var img = instance.GetComponentInChildren<Image>();
        if (img != null) img.sprite = ge.CardSO.CardImage;

        var tmp = instance.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = GetDisplayText(ge);

        _displayedCards[ge] = rt;
        layout.AddCard(rt);
    }

    private void HandleTurnTicked(GlobalEffector ge)
    {
        if (!_displayedCards.TryGetValue(ge, out var rt)) return;
        var tmp = rt.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = GetDisplayText(ge);
    }

    private static string GetDisplayText(GlobalEffector ge) =>
        ge.IsPermanent ? ge.CardSO.CardName : $"{ge.CardSO.CardName} ({ge.RemainingTurns}턴)";

    private void HandleDeactivated(GlobalEffector ge)
    {
        if (!_displayedCards.TryGetValue(ge, out var rt)) return;

        _displayedCards.Remove(ge);
        layout.RemoveCard(rt);

        var cg = rt.gameObject.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

        cg.DOFade(0f, destroyDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(rt.gameObject));
    }
}
