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


    [SerializeField] private GameObject TimeReversalPanel;

    private Action onYes;
    private Action onNo;

    public void ShowTimeReversal(Action yes, Action no)
    {
        onYes = yes;
        onNo = no;

        TimeReversalPanel.SetActive(true);
    }

    public void OnClickTimeReversalYes()
    {
        TimeReversalPanel.SetActive(false);
        onYes?.Invoke();
    }

    public void OnClickTimeReversalNo()
    {
        TimeReversalPanel.SetActive(false);
        onNo?.Invoke();
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


    [SerializeField] private GameObject EndGamePanel;
    [SerializeField] private GameObject BGPanel;
    [SerializeField] private TextMeshProUGUI resultText;

    public void ShowEndGamePanel(GameResult result)
    {
        BGPanel.SetActive(true);
        switch (result)
        {
            case GameResult.WhiteWin:
                resultText.text = "플레이어 승리";
                break;
            case GameResult.BlackWin:
                resultText.text = "AI 승리";
                break;
            case GameResult.Draw:
                resultText.text = "무승부";
                break;
        }

        EndGamePanel.SetActive(true);
    }

    public void OnClickNext()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("RewardScene");
    }
}
