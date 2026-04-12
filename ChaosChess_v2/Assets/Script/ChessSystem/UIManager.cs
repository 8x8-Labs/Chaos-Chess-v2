using System;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private PromotionPanel promotionPanel;

    public void ShowPromotion(Action<char> callback)
    {
        promotionPanel.Show(callback);
    }


    [SerializeField] private TimeReversalPanel timeReversalPanel;

    public void ShowTimeReversal(Action yes, Action no)
    {
        timeReversalPanel.Show(yes, no);
    }


    [SerializeField] private AwakenPanel awakenPanel;

    public void ShowAwaken(Action callback)
    {
        awakenPanel.Show(callback);
    }

    public void HideAwakenButton()
    {
        awakenPanel.Hide();
    }


    [SerializeField] private EndGamePanel endGamePanel;

    public void ShowEndGame(GameResult result)
    {
        endGamePanel.Show(result);
    }
}
