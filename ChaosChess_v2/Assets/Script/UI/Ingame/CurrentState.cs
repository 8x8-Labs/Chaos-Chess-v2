using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrentState : MonoBehaviour
{
    private PieceColor currentColor => GameManager.Instance.turnColor;
    [SerializeField] private Image turnColor;
    [SerializeField] private Sprite whiteColor;
    [SerializeField] private Sprite blackColor;

    private void Start() => GameManager.Instance.OnHalfTurnChanged += OnChangedState;

    private void OnDestroy() => GameManager.Instance.OnHalfTurnChanged -= OnChangedState;

    public void OnChangedState()
    {
        if (currentColor == PieceColor.Black)
        {
            turnColor.sprite = blackColor;
        }
        else
        {
            turnColor.sprite = whiteColor;
        }
    }
}
