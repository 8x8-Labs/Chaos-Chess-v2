using TMPro;
using UnityEngine;

public class EndGamePanel : ButtonPanel
{
    [SerializeField] private TextMeshProUGUI resultText;

    public void Show(GameResult result)
    {
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

        EnablePanel();
    }
}