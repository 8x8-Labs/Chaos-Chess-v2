using TMPro;
using UnityEngine;

public class EndGamePanel : ButtonPanel
{
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private UIButton nextButton;

    public void Show(GameResult result)
    {
        switch (result)
        {
            case GameResult.WhiteWin:
                resultText.text = "승리";
                resultText.color = Color.yellow;
                break;
            case GameResult.BlackWin:
                resultText.color = Color.gray;
                resultText.text = "패배";
                nextButton.SetNextScene("ResultScene");
                break;
            case GameResult.Draw:
                resultText.text = "무승부";
                break;
        }

        if (MapManager.Instance.currentFloor >= MapManager.Instance.totalFloors)
            nextButton.SetNextScene("ResultScene");

        EnablePanel();
    }
}