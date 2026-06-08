using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float bgmFadeDuration = 0.8f;
    [SerializeField] private float loadingDisplayDelay = 0.25f;

    private static SceneLoadManager instance;

    public static SceneLoadManager Instance => instance;

    private bool isLoading;
    [SerializeField] private SceneLoadingOverlayBase basicOverlay;
    [SerializeField] private SceneLoadingOverlayBase rainOverlay;
    private SceneLoadingOverlayBase basicOverlayInstance;
    private SceneLoadingOverlayBase rainOverlayInstance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateLoadingOverlay();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName))
            return;

        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private SceneLoadingOverlayBase GetOverlayFor(string sceneName)
    {
        if (sceneName == "MapScene" && rainOverlayInstance != null)
            return rainOverlayInstance;

        return basicOverlayInstance;
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        isLoading = true;

        SoundManager soundManager = SoundManager.Instance;
        bool forceBgmFade = SceneManager.GetActiveScene().name == "ResultScene" && sceneName == "MainScene";
        bool transitionBgm = soundManager != null &&
                             (forceBgmFade || soundManager.ShouldTransitionBGM(sceneName));
        Tween bgmFadeOut = soundManager?.BeginSceneTransitionFadeOut(
            sceneName,
            forceBgmFade,
            bgmFadeDuration);

        if (transitionBgm)
            yield return null;

        SceneLoadingOverlayBase overlayUI = GetOverlayFor(sceneName);
        if (overlayUI != null)
        {
            overlayUI.gameObject.SetActive(true);
            overlayUI.Initialize();
        }

        yield return null; // 오버레이가 렌더링된 후 페이드 시작
        Tween fadeIn = overlayUI?.FadeTo(1f, fadeDuration);
        if (fadeIn != null)
            yield return fadeIn.WaitForCompletion();

        if (bgmFadeOut != null && bgmFadeOut.IsActive() && !bgmFadeOut.IsComplete())
            yield return bgmFadeOut.WaitForCompletion();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        yield return WaitForReadyToActivate(overlayUI, operation);

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        soundManager?.ApplySceneBGM(sceneName, forceBgmFade, bgmFadeDuration);

        if (transitionBgm)
            yield return null;

        Tween fadeOut = overlayUI?.FadeTo(0f, fadeDuration);
        if (fadeOut != null)
            yield return fadeOut.WaitForCompletion();

        overlayUI?.SetLoadingContentVisible(false);
        overlayUI?.gameObject.SetActive(false);
        isLoading = false;
    }

    private IEnumerator WaitForReadyToActivate(SceneLoadingOverlayBase overlay, AsyncOperation operation)
    {
        float elapsed = 0f;
        bool isLoadingContentVisible = false;

        while (operation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            if (!isLoadingContentVisible && elapsed >= loadingDisplayDelay)
            {
                overlay?.SetLoadingContentVisible(true);
                isLoadingContentVisible = true;
            }

            overlay?.SetProgress(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        overlay?.SetAlpha(1f);
        overlay?.SetProgress(1f);
    }

    private void CreateLoadingOverlay()
    {
        if (basicOverlay != null)
        {
            basicOverlayInstance = Instantiate(basicOverlay, transform);
            basicOverlayInstance.Initialize();
            basicOverlayInstance.gameObject.SetActive(false);
        }

        if (rainOverlay != null)
        {
            rainOverlayInstance = Instantiate(rainOverlay, transform);
            rainOverlayInstance.Initialize();
            rainOverlayInstance.gameObject.SetActive(false);
        }
    }
}
