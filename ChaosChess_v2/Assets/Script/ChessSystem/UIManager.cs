using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject PromotionPanel;

    private System.Action<char> onSelected;

    void Awake()
    {
        PromotionPanel.SetActive(false);
    }

    public void Show(System.Action<char> callback)
    {
        onSelected = callback;
        PromotionPanel.SetActive(true);
    }

    public void OnClickQueen() => Select('q');
    public void OnClickRook() => Select('r');
    public void OnClickBishop() => Select('b');
    public void OnClickKnight() => Select('n');

    private void Select(char type)
    {
        PromotionPanel.SetActive(false);
        onSelected?.Invoke(type);
    }


    [SerializeField] private GameObject TimeReversalPanel;

    private System.Action onYes;
    private System.Action onNo;

    public void ShowTimeReversal(System.Action yes, System.Action no)
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


    [SerializeField] private GameObject AwakenPanel;

    private System.Action onAwakenClick;

    public void HideAwakenButton()
    {
        AwakenPanel.SetActive(false);
        onAwakenClick = null;
    }

    public void ShowAwakenButton(System.Action callback)
    {
        onAwakenClick = callback;
        AwakenPanel.SetActive(true);
    }

    public void OnClickAwaken()
    {
        AwakenPanel.SetActive(false);
        onAwakenClick?.Invoke();
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
