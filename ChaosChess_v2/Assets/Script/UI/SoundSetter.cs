using UnityEngine;
using UnityEngine.UI;

public class SoundSetter : MonoBehaviour
{
    private SoundManager soundManager;

    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider bgmSlider;

    private void Start()
    {
        soundManager = SoundManager.Instance;
    }

    public void EnableSetter()
    {
        bgmSlider.value = soundManager.GetBGMVolume();
        sfxSlider.value = soundManager.GetSFXVolume();
    }

    public void DisableSetter() { }

    public void SetBGM(float val) => soundManager.BGSoundVolume(val);
    public void SetSFX(float val) => soundManager.SFXSoundVolume(val);
}
