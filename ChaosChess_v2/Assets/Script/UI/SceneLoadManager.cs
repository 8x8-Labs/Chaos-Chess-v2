using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float loadingDisplayDelay = 0.25f;

    private static SceneLoadManager instance;

    public static SceneLoadManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject loader = new GameObject(nameof(SceneLoadManager));
                loader.AddComponent<SceneLoadManager>();
            }

            return instance;
        }
    }

    private bool isLoading;
    [SerializeField] private SceneLoadingOverlayUI overlayPrefab;
    private SceneLoadingOverlayUI overlayUI;

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
        if (isLoading)
            return;

        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        isLoading = true;
        overlayUI?.Initialize();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        Tween fadeIn = overlayUI?.FadeTo(1f, fadeDuration);
        yield return WaitForReadyToActivate(operation, fadeIn);

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        Tween fadeOut = overlayUI?.FadeTo(0f, fadeDuration);
        if (fadeOut != null)
            yield return fadeOut.WaitForCompletion();

        overlayUI?.SetLoadingContentVisible(false);
        isLoading = false;
    }

    private IEnumerator WaitForReadyToActivate(AsyncOperation operation, Tween fadeTween)
    {
        float elapsed = 0f;
        bool isLoadingContentVisible = false;

        while (operation.progress < 0.9f || IsTweenRunning(fadeTween))
        {
            elapsed += Time.unscaledDeltaTime;

            if (!isLoadingContentVisible && elapsed >= loadingDisplayDelay)
            {
                overlayUI?.SetLoadingContentVisible(true);
                isLoadingContentVisible = true;
            }

            overlayUI?.SetProgress(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        overlayUI?.SetAlpha(1f);
        overlayUI?.SetProgress(1f);
    }

    private bool IsTweenRunning(Tween tween)
    {
        return tween != null && tween.IsActive() && !tween.IsComplete();
    }

    private void CreateLoadingOverlay()
    {
        overlayUI = Instantiate(overlayPrefab, transform);
        overlayUI.Initialize();
    }
}
