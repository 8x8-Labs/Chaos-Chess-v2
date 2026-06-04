using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public enum GameMode
{
    Run,
    Practice
}

public class GameCycleManager : MonoBehaviour
{
    public static GameCycleManager Instance;
    public GameMode CurrentMode { get; private set; } = GameMode.Run;
    public bool IsPracticeMode => CurrentMode == GameMode.Practice;

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
        CurrentMode = GameMode.Run;
        PlayerState.Instance?.InitializeRun();
        MapManager.Instance?.Init();
    }

    /// <summary>
    /// 저장된 런을 불러와 이어한다.
    /// Load()는 JSON을 CurrentRunData에 캐싱만 하고, 씬 로드 완료 후
    /// OnSavedSceneLoaded()에서 ApplyLoadedData()를 호출해 실제 복원한다.
    /// </summary>
    public void ContinueRun()
    {
        CurrentMode = GameMode.Run;
        SaveManager.Instance.Load();
        SceneManager.sceneLoaded += OnSavedSceneLoaded;
        SceneLoadManager.Instance.LoadScene(SaveManager.Instance.GetSavedScene());
    }

    private void OnSavedSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSavedSceneLoaded;
        SaveManager.Instance?.ApplyLoadedData();
    }

    public void StartPractice(PracticeDifficulty difficulty)
    {
        CurrentMode = GameMode.Practice;
        PlayerState.Instance.InitializeRun();
        GiveAllCards();
        MapManager.Instance.StartPractice(difficulty);
    }

    public string GetEndGameSceneName()
    {
        return IsPracticeMode ? "MainScene" : "RewardScene";
    }

    private void GiveAllCards()
    {
        foreach (GameObject card in CardRandomizerManager.Instance.AllCards)
        {
            PlayerState.Instance.AddCard(card);
        }
    }
}
