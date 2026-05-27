using UnityEngine;
using UnityEngine.UI;

public class LoadingSceneUI : MonoBehaviour
{
    public static LoadingSceneUI Instance { get; private set; }

    [SerializeField] private Slider progressSlider;

    private void Awake()
    {
        Instance = this;
        SetProgress(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetProgress(float progress)
    {
        if (progressSlider != null)
            progressSlider.value = Mathf.Clamp01(progress);
    }
}
