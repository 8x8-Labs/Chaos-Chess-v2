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

        uiCardPanel.SetRequestedCardCount(rewardCardCount);

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
