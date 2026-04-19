using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class CardRewardManager : MonoBehaviour
{
    private int _rewardCardCount = 0;

    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private UICardRandomizer uiCardRandomizer;

    [SerializeField] private TextMeshProUGUI cardRewardText;

    void Start()
    {
        if (PlayerState.Instance == null)
            return;

        if (PlayerState.Instance.CurGameResult == GameResult.WhiteWin)
            _rewardCardCount = 3;
        else if (PlayerState.Instance.CurGameResult == GameResult.Draw)
            _rewardCardCount = 1;

        List<GameObject> ownedCards = PlayerState.Instance.CardPool.ToList<GameObject>();
        List<GameObject> randomCards = uiCardRandomizer.GetRandomCards(ownedCards, _rewardCardCount);

        // 모든 카드를 다 가지고 있을 수 있음
        _rewardCardCount = randomCards.Count;
        uiCardPanel.cardCount = _rewardCardCount;

        UICardAnim[] uiCards = uiCardPanel.GetUICards;
        for (int i = 0; i < _rewardCardCount; i++)
        {
            uiCards[i].CardPreFab = randomCards[i];
        }

        cardRewardText.text = $"카드 {_rewardCardCount}개 얻기";
    }
}
