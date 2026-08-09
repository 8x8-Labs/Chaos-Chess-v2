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

    /// <summary>
    /// 이번 매치에서 플레이어가 맡을 진영입니다. 런/연습은 백 고정이며,
    /// 멀티에서는 매칭 결과로 배정받은 색을 SetPlayerColor로 넣습니다.
    /// GameManager가 매치 시작 시 이 값을 읽어갑니다.
    /// </summary>
    public PieceColor PlayerColor { get; private set; } = PieceColor.White;

    [Header("디버그")]
    [Tooltip("체크하면 런/연습을 흑으로 시작합니다. 보드 시점 검증용이며, 멀티 매칭이 붙으면 제거합니다.")]
    [SerializeField] private bool debugPlayAsBlack;

    /// <summary>런/연습 진입 시 사용할 기본 진영입니다. 평소에는 백입니다.</summary>
    private PieceColor DefaultPlayerColor => debugPlayAsBlack ? PieceColor.Black : PieceColor.White;

    /// <summary>플레이어 진영을 지정합니다. 매치가 시작되기 전에 호출해야 합니다.</summary>
    public void SetPlayerColor(PieceColor color)
    {
        PlayerColor = color;
    }

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
        PlayerColor = DefaultPlayerColor;
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
        PlayerColor = DefaultPlayerColor;
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
        PlayerColor = DefaultPlayerColor;
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
