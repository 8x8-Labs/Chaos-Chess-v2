using UnityEngine;
using UnityEngine.UI;

public class ToggleUI : Toggle
{
    [SerializeField] private Image IconImage;
    [SerializeField] private Sprite OnIcon;
    [SerializeField] private Sprite OffIcon;
    [SerializeField] private SliderUI LinkedSlider;

    protected override void Awake()
    {
        base.Awake();
        onValueChanged.AddListener(HandleValueChanged);
    }

    private void HandleValueChanged(bool isOn)
    {
        if (IconImage != null)
            IconImage.sprite = isOn ? OnIcon : OffIcon;

        LinkedSlider?.SetActive(isOn);
    }
}
