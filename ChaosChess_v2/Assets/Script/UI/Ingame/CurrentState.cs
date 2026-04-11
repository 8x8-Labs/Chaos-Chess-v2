using TMPro;
using UnityEngine;

public class CurrentState : MonoBehaviour
{
    private PieceColor currentColor => GameManager.Instance.turnColor;

    [SerializeField] private TMP_Text turnText;

    private void Start() => GameManager.Instance.OnHalfTurnChanged += OnChangedState;

    private void OnDestroy() => GameManager.Instance.OnHalfTurnChanged -= OnChangedState;

    public void OnChangedState()
    {
        if (currentColor == PieceColor.Black)
        {
            turnText.text = "BLACK";
            turnText.color = Color.black;
        }
        else
        {
            turnText.text = "WHITE";
            turnText.color = Color.white;
        }
    }
}
