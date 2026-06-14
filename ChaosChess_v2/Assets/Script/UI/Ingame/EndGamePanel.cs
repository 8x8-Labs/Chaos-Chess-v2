using TMPro;
using UnityEngine;

public class EndGamePanel : ButtonPanel
{
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private UIButton nextButton;
    [Tooltip("승리는 0, 패배는 1, 무승부는 2")]
    [SerializeField] private AudioClip EndgameSFX;

    public void Show(GameResult result)
    {
        SoundManager.Instance?.SFXPlay("EndgameSFX", EndgameSFX);
        switch (result)
        {
            case GameResult.WhiteWin:
                resultText.text = "승리";
                resultText.color = Color.yellow;
                break;
            case GameResult.BlackWin:
                resultText.color = Color.gray;
                resultText.text = "패배";
                break;
            case GameResult.Draw:
                resultText.text = "무승부";
                break;
        }

        EnablePanel();
    }
}
