using UnityEngine;

public class SelectorUI : MonoBehaviour
{
    private UIButton executeButton;

    private void Start() => executeButton = GetComponent<UIButton>();
    public void EnableButtonState() => executeButton.interactable = true;
    public void DisableButtonState() => executeButton.interactable = false;
    public void UpdateButtonState(bool value) => executeButton.interactable = value;
    
}
