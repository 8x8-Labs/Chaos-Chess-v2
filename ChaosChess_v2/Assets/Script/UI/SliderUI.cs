using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text context;

    [Header("Sprites")]
    [SerializeField] private Sprite fullIcon;
    [SerializeField] private Sprite mediumIcon;
    [SerializeField] private Sprite lowIcon;

    private float _currentValue;

    public void SetActive(bool active)
    {
        if (active)
            iconImage.sprite = lowIcon;
        else
            SetIconSprite(_currentValue);
    }

    public void SetIconSprite(float value)
    {
        _currentValue = value;

        if (value >= 0.5f)
            iconImage.sprite = fullIcon;
        else if (value <= 0f)
            iconImage.sprite = lowIcon;
        else
            iconImage.sprite = mediumIcon;
    }

    public void SetContext(float value)
    {
        context.text = $"{Mathf.RoundToInt(value * 100)}%";
    }
}
