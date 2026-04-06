using UnityEngine;
using UnityEngine.UI;

public class SoundSetter : MonoBehaviour
{
    private SoundManager soundManager;

    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Toggle sfxMuteToggle;
    [SerializeField] private Toggle bgmMuteToggle;

    private void Start()
    {
        soundManager = SoundManager.Instance;
    }

    public void EnableSetter()
    {
        bgmSlider.value = soundManager.GetBGMVolume();
        sfxSlider.value = soundManager.GetSFXVolume();
        bgmSlider.interactable = !soundManager.GetBGMMute();
        sfxSlider.interactable = !soundManager.GetSFXMute();
        bgmMuteToggle.isOn = soundManager.GetBGMMute();
        sfxMuteToggle.isOn = soundManager.GetSFXMute();
    }

    public void DisableSetter() { }

    public void SetBGM(float val) => soundManager.BGSoundVolume(val);
    public void SetSFX(float val) => soundManager.SFXSoundVolume(val);

    public void MuteBGM(bool value)
    {
        soundManager.MuteBGM(value);
        bgmSlider.interactable = !value;
    }
    public void MuteSFX(bool value)
    {
        soundManager.MuteSFX(value);
        sfxSlider.interactable = !value;
    }
}
