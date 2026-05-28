using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.18f;
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
        SceneLoadingOverlayBase overlayUI = GetOverlayFor(sceneName);
        if (overlayUI != null)
        {
            overlayUI.gameObject.SetActive(true);
            overlayUI.Initialize();
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        yield return null; // DOTween이 같은 프레임 내 Update를 이미 처리했을 경우를 위한 1프레임 대기
        Tween fadeIn = overlayUI?.FadeTo(1f, fadeDuration);
        yield return WaitForReadyToActivate(overlayUI, operation, fadeIn);

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        Tween fadeOut = overlayUI?.FadeTo(0f, fadeDuration);
        if (fadeOut != null)
            yield return fadeOut.WaitForCompletion();

        overlayUI?.SetLoadingContentVisible(false);
        overlayUI?.gameObject.SetActive(false);
        isLoading = false;
    }

    private IEnumerator WaitForReadyToActivate(SceneLoadingOverlayBase overlay, AsyncOperation operation, Tween fadeTween)
    {
        float elapsed = 0f;
        bool isLoadingContentVisible = false;

        while (operation.progress < 0.9f || IsTweenRunning(fadeTween))
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

    private bool IsTweenRunning(Tween tween)
    {
        return tween != null && tween.IsActive() && !tween.IsComplete();
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
