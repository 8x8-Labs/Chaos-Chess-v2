using UnityEngine;
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
    /// StartGame()과 달리 InitializeRun/Init()을 호출하지 않고
    /// SaveManager.Load()로 기존 상태를 복원한 뒤 MapScene으로 이동한다.
    /// </summary>
    public void ContinueRun()
    {
        CurrentMode = GameMode.Run;
        SaveManager.Instance.Load();
        SceneLoadManager.Instance.LoadScene("MapScene");
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
