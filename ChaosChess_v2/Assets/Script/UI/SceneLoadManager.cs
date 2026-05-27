using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : MonoBehaviour
{
    private const string LoadingSceneName = "LoadingScene";

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

    public void LoadScene(string sceneName) => StartCoroutine(LoadSceneCoroutine(sceneName));

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        if (isLoading)
            yield break;

        isLoading = true;

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.name != LoadingSceneName && sceneName != LoadingSceneName)
        {
            AsyncOperation loadingSceneOperation = SceneManager.LoadSceneAsync(LoadingSceneName);
            if (loadingSceneOperation == null)
            {
                isLoading = false;
                yield break;
            }

            while (!loadingSceneOperation.isDone)
                yield return null;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        SetProgress(1f);

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        isLoading = false;
    }

    private void SetProgress(float progress)
    {
        LoadingSceneUI.Instance?.SetProgress(progress);
    }
}
