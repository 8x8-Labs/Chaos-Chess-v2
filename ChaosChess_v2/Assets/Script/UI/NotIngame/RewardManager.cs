using UnityEngine;

public class RewardManager : MonoBehaviour
{
    private int _rewardCardCount = 0;

    [SerializeField] private UICardPanel uiCardPanel;

    void Start()
    {
        if (PlayerState.Instance != null)
        {
            if (PlayerState.Instance.CurGameResult == GameResult.WhiteWin)
                _rewardCardCount = 3;
            else if (PlayerState.Instance.CurGameResult == GameResult.Draw)
                _rewardCardCount = 1;
        }
        uiCardPanel.cardCount = _rewardCardCount;
    }
}
