using UnityEngine;
using UnityEngine.SceneManagement;

public class GamaCycleManager : MonoBehaviour
{
    public static GamaCycleManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartGame()
    {
        PlayerState.Instance?.ResetRun();
        MapManager.Instance?.Init();

        SceneManager.LoadScene("MapScene");
    }
}
