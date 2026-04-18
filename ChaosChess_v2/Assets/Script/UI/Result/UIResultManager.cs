using TMPro;
using UnityEngine;

public class UIResultManager : MonoBehaviour
{
    void Start()
    {
        UpdateRecordText();
    }


    [SerializeField] private TextMeshProUGUI winValue;
    [SerializeField] private TextMeshProUGUI drawValue;
    [SerializeField] private TextMeshProUGUI loseValue;

    private void UpdateRecordText()
    {
        if (PlayerState.Instance == null) return;

        winValue.text = PlayerState.Instance.WinCount.ToString();
        drawValue.text = PlayerState.Instance.DrawCount.ToString();
        loseValue.text = PlayerState.Instance.LoseCount.ToString();
    }
}
