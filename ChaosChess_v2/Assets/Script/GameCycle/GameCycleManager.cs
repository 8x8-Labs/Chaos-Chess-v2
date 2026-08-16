using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public enum GameMode
{
    Run,
    Practice,
    Multiplayer
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

    [Tooltip("체크하면 런/연습을 멀티플레이 모드로 시작해 RemoteTurnProvider를 사용합니다. 매칭이 붙으면 제거합니다.")]
    [SerializeField] private bool debugMultiplayerMode;

    /// <summary>
    /// 런/연습 진입 시 사용할 기본 진영입니다. 평소에는 백입니다.
    ///
    /// 멀티 모드에서는 두 인스턴스가 서로 다른 색을 잡아야 하는데, MPPM은 씬 에셋을 공유해서
    /// debugPlayAsBlack으로는 갈라낼 수 없습니다. 그래서 역할(호스트/게스트)에서 색을 파생시킵니다.
    /// 실제 매칭이 붙으면 서버가 배정한 색을 SetPlayerColor로 넣게 됩니다.
    /// </summary>
    private PieceColor DefaultPlayerColor
    {
        get
        {
            if (CurrentMode == GameMode.Multiplayer && MppmMatchRole.IsAvailable)
                return MppmMatchRole.Color;

            return debugPlayAsBlack ? PieceColor.Black : PieceColor.White;
        }
    }

    /// <summary>
    /// 진입할 모드를 결정합니다. 디버그 토글이 켜져 있으면 멀티로 시작해 RemoteTurnProvider를 태웁니다.
    /// 실제 매칭이 붙으면 이 우회는 제거하고 매칭 결과가 모드를 정합니다.
    /// </summary>
    private GameMode ResolveMode(GameMode normalMode)
    {
        return debugMultiplayerMode ? GameMode.Multiplayer : normalMode;
    }

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
        CurrentMode = ResolveMode(GameMode.Run);
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
        CurrentMode = ResolveMode(GameMode.Run);
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
        CurrentMode = ResolveMode(GameMode.Practice);
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
