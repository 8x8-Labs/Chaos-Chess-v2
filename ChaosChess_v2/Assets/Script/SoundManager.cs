using System.Collections;
using UnityEngine;
using UnityEngine.Audio;


public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;

    public AudioMixer audioMixer;
    [SerializeField] private AudioSource bgmSource;

    public static SoundManager Instance
    {
        get
        {
            if (instance == null) instance = new SoundManager();
            return instance;
        }
    }
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        if (PlayerPrefs.HasKey("BGMSound"))
        {
            BGSoundVolume(PlayerPrefs.GetFloat("BGMSound"));
        }
        else
        {
            BGSoundVolume(1);
        }

        if (PlayerPrefs.HasKey("SFXSound"))
        {
            SFXSoundVolume(PlayerPrefs.GetFloat("SFXSound"));
        }
        else
        {
            SFXSoundVolume(1);
        }

        MuteBGM(GetBGMMute());
        MuteSFX(GetSFXMute());
    }

    // ── BGM 재생 제어 ──────────────────────────────────────

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PauseBGM()
    {
        bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        bgmSource.UnPause();
    }

    /// <summary>
    /// 현재 BGM을 페이드 아웃한 뒤 새 클립으로 페이드 인합니다.
    /// </summary>
    public void SwitchBGM(AudioClip newClip, float duration = 0.8f)
    {
        StartCoroutine(SwitchBGMCoroutine(newClip, duration));
    }

    private IEnumerator SwitchBGMCoroutine(AudioClip newClip, float duration)
    {
        if (bgmSource.isPlaying)
        {
            yield return FadeOutCoroutine(bgmSource, duration);
            bgmSource.Stop();
        }

        bgmSource.clip = newClip;
        bgmSource.loop = true;
        bgmSource.volume = 0f;
        bgmSource.Play();

        yield return FadeInCoroutine(bgmSource, duration);
    }

    // ── BGM 페이드 (내부 bgmSource 사용) ─────────────────

    public void BgFadeIn(float duration = 0.8f)
    {
        StartCoroutine(FadeInCoroutine(bgmSource, duration));
    }

    public void BgFadeOut(float duration = 0.8f)
    {
        StartCoroutine(FadeOutCoroutine(bgmSource, duration));
    }

    // ── BGM 페이드 (외부 AudioSource 사용, 기존 오버로드 유지) ──

    public void BgFadeIn(AudioSource BgPlayer)
    {
        StartCoroutine(FadeInCoroutine(BgPlayer, 0.8f));
    }
    public void BgFadeInCustom(AudioSource BgPlayer, float volume, float time)
    {
        StartCoroutine(FadeInCoroutine(BgPlayer, time, volume));
    }
    public void BgFadeOut(AudioSource BgPlayer)
    {
        StartCoroutine(FadeOutCoroutine(BgPlayer, 0.8f));
    }
    public void BgFadeOutCustom(AudioSource BgPlayer, float time)
    {
        StartCoroutine(FadeOutCoroutine(BgPlayer, time));
    }

    // ── 페이드 코루틴 ─────────────────────────────────────

    private IEnumerator FadeInCoroutine(AudioSource source, float duration, float targetVolume = 1f)
    {
        float elapsed = 0f;
        float startVolume = source.volume;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }
        source.volume = targetVolume;
    }

    private IEnumerator FadeOutCoroutine(AudioSource source, float duration)
    {
        float elapsed = 0f;
        float startVolume = source.volume;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        source.volume = 0f;
    }

    // ── SFX 재생 ──────────────────────────────────────────

    public void SFXPlay(string sfxName, AudioClip clip)
    {
        GameObject go = new GameObject(sfxName + "Sound");
        AudioSource source = go.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = audioMixer.FindMatchingGroups("SFX")[0];
        source.clip = clip;
        source.Play();

        StartCoroutine(DestroyAfterRealtime(go, clip.length));
    }

    // ── 볼륨 설정 / 조회 ──────────────────────────────────

    public void MuteBGM(bool mute)
    {
        bgmSource.mute = mute;
        PlayerPrefs.SetInt("BGMMute", mute ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void MuteSFX(bool mute)
    {
        audioMixer.SetFloat("SFXSound", mute ? -80f : Mathf.Log10(Mathf.Max(GetSFXVolume(), 0.0001f)) * 20);
        PlayerPrefs.SetInt("SFXMute", mute ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool GetBGMMute() => PlayerPrefs.GetInt("BGMMute", 0) == 1;
    public bool GetSFXMute() => PlayerPrefs.GetInt("SFXMute", 0) == 1;

    public void BGSoundVolume(float val)
    {
        val = Mathf.Max(val, 0.0001f);
        float n = Mathf.Log10(val) * 20;
        audioMixer.SetFloat("BGMSound", n);
        PlayerPrefs.SetFloat("BGMSound", val);
        PlayerPrefs.Save();
    }
    public void SFXSoundVolume(float val)
    {
        val = Mathf.Max(val, 0.0001f);
        float n = Mathf.Log10(val) * 20;
        audioMixer.SetFloat("SFXSound", n);
        PlayerPrefs.SetFloat("SFXSound", val);
        PlayerPrefs.Save();
    }

    public float GetBGMVolume()
    {
        return PlayerPrefs.HasKey("BGMSound") ? PlayerPrefs.GetFloat("BGMSound") : 1f;
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.HasKey("SFXSound") ? PlayerPrefs.GetFloat("SFXSound") : 1f;
    }

    private IEnumerator DestroyAfterRealtime(GameObject go, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Destroy(go);
    }
}
