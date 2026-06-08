using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;


public class SoundManager : MonoBehaviour
{
    [System.Serializable]
    private class SceneBgmMapping
    {
        public string sceneName;
        public AudioClip bgmClip;
    }

    private const float DefaultSceneBgmFadeDuration = 0.8f;
    private static SoundManager instance;

    public AudioMixer audioMixer;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip[] cardUseSFXByTier;
    [SerializeField] private float cardUseSFXVolume = 1f;
    [SerializeField] private List<SceneBgmMapping> sceneBgmMappings = new();
    [SerializeField] private float sceneBgmFadeDuration = 0.8f;

    private Tween bgmFadeTween;
    private float SceneBgmFadeDuration => sceneBgmFadeDuration > 0f ? sceneBgmFadeDuration : DefaultSceneBgmFadeDuration;

    public static SoundManager Instance => instance;
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
        PlayInitialBGM();
    }

    private void PlayInitialBGM()
    {
        if (bgmSource == null || bgmSource.clip == null || bgmSource.isPlaying)
            return;

        bgmSource.loop = true;
        bgmSource.volume = 0f;
        bgmSource.Play();
        BgFadeIn(SceneBgmFadeDuration);
    }

    // ── BGM 재생 제어 ──────────────────────────────────────

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (bgmSource == null || clip == null)
            return;

        bgmFadeTween?.Kill();
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = 1f;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource == null)
            return;

        bgmFadeTween?.Kill();
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
        SwitchBGM(newClip, duration, true);
    }

    public void SwitchBGM(AudioClip newClip, float duration, bool fadeOutCurrent)
    {
        if (bgmSource == null || newClip == null)
            return;

        bgmFadeTween?.Kill();

        if (bgmSource.clip == newClip)
        {
            if (!bgmSource.isPlaying)
                PlayBGM(newClip);
            return;
        }

        float fadeDuration = Mathf.Max(0f, duration);
        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        if (fadeOutCurrent && bgmSource.isPlaying)
        {
            sequence.Append(bgmSource.DOFade(0f, fadeDuration));
        }

        sequence.AppendCallback(() =>
        {
            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.loop = true;
            bgmSource.volume = 0f;
            bgmSource.Play();
        });
        sequence.Append(bgmSource.DOFade(1f, fadeDuration));
        bgmFadeTween = sequence;
    }

    public bool ShouldTransitionBGM(string sceneName)
    {
        AudioClip sceneBgmClip = GetSceneBGM(sceneName);
        return bgmSource != null && sceneBgmClip != null && bgmSource.clip != sceneBgmClip;
    }

    public Tween BeginSceneTransitionFadeOut(string sceneName, bool forceFade = false, float? duration = null)
    {
        if (!forceFade && !ShouldTransitionBGM(sceneName))
            return null;

        return BgFadeOut(duration ?? SceneBgmFadeDuration);
    }

    public void ApplySceneBGM(string sceneName, bool restart = false, float? duration = null)
    {
        AudioClip sceneBgmClip = GetSceneBGM(sceneName);
        if (bgmSource == null || sceneBgmClip == null)
            return;

        float fadeDuration = duration ?? SceneBgmFadeDuration;

        if (bgmSource.clip == sceneBgmClip && !restart)
        {
            if (!bgmSource.isPlaying)
                bgmSource.Play();

            BgFadeIn(fadeDuration);
            return;
        }

        if (restart)
        {
            bgmFadeTween?.Kill();
            bgmSource.Stop();
            bgmSource.clip = sceneBgmClip;
            bgmSource.loop = true;
            bgmSource.time = 0f;
            bgmSource.volume = 0f;
            bgmSource.Play();
            BgFadeIn(fadeDuration);
            return;
        }

        SwitchBGM(sceneBgmClip, fadeDuration, false);
    }

    private AudioClip GetSceneBGM(string sceneName)
    {
        foreach (SceneBgmMapping mapping in sceneBgmMappings)
        {
            if (mapping.sceneName == sceneName)
                return mapping.bgmClip;
        }

        return null;
    }

    // ── BGM 페이드 (내부 bgmSource 사용) ─────────────────

    public void BgFadeIn(float duration = 0.8f)
    {
        if (bgmSource == null)
            return;

        bgmFadeTween?.Kill();
        bgmFadeTween = bgmSource.DOFade(1f, Mathf.Max(0f, duration)).SetUpdate(true);
    }

    public Tween BgFadeOut(float duration = 0.8f)
    {
        if (bgmSource == null)
            return null;

        bgmFadeTween?.Kill();
        bgmFadeTween = bgmSource.DOFade(0f, Mathf.Max(0f, duration)).SetUpdate(true);
        return bgmFadeTween;
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
        SFXPlay(sfxName, clip, 1f);
    }

    public void SFXPlay(string sfxName, AudioClip clip, float volume)
    {
        GameObject go = new GameObject(sfxName + "Sound");
        AudioSource source = go.AddComponent<AudioSource>();
        if (audioMixer != null)
        {
            var groups = audioMixer.FindMatchingGroups("SFX");
            if (groups != null && groups.Length > 0)
                source.outputAudioMixerGroup = groups[0];
        }
        source.clip = clip;
        source.volume = volume;
        source.Play();

        StartCoroutine(DestroyAfterRealtime(go, clip.length));
    }

    public void PlayCardUseSFX(Tier tier)
    {
        AudioClip clip = GetCardUseSFX(tier);
        if (clip == null)
            return;

        SFXPlay("CardUseSFX", clip, cardUseSFXVolume);
    }

    private AudioClip GetCardUseSFX(Tier tier)
    {
        int index = (int)tier;
        if (cardUseSFXByTier == null || index < 0 || index >= cardUseSFXByTier.Length)
            return null;

        return cardUseSFXByTier[index];
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
