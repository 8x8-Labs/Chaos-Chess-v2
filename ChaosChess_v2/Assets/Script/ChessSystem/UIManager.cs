using System;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private PromotionPanel promotionPanel;

    private Action<char> onSelected;

    public void ShowPromotion(Action<char> callback)
    {
        Debug.Log("?");
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
