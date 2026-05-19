using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardRewardManager : MonoBehaviour
{
    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private TextMeshProUGUI cardRewardText;

    private void Start()
    {
        if (PlayerState.Instance == null)
            return;

        int rewardCardCount = ResolveRewardCardCount();
        if (rewardCardCount <= 0 || uiCardPanel == null)
            return;

        List<GameObject> ownedCards = new List<GameObject>(PlayerState.Instance.CardPool);
        List<GameObject> rewardCards =
            CardRandomizerManager.Instance.GetRandomCardsFromAll(ownedCards, rewardCardCount);

        foreach (var card in rewardCards)
        {
            PlayerState.Instance?.AddCard(card);
        }

        uiCardPanel.SetCards(rewardCards);

        if (cardRewardText != null)
            cardRewardText.text = $"카드 {rewardCardCount}개 얻기";
    }

    private int ResolveRewardCardCount()
    {
        if (PlayerState.Instance.CurGameResult == GameResult.WhiteWin)
            return 3;

        if (PlayerState.Instance.CurGameResult == GameResult.Draw)
            return 1;

        return 0;
    }
}
