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

    [Header("멀티플레이 연결 (매칭이 붙으면 제거)")]
    [Tooltip("어느 전송 계층으로 대전할지 고릅니다. Loopback은 네트워크 없이 흐름만 검증합니다.")]
    [SerializeField] private MatchTransportKind multiplayerTransport = MatchTransportKind.Relay;

    [Tooltip("연결 UI로 정한 역할이 여기 반영됩니다. UI 없이 인스펙터 값으로 바로 테스트할 때도 이 값이 쓰이고, " +
             "에디터에서 MPPM으로 띄웠으면 MPPM 판정이 대신 우선합니다.")]
    [SerializeField] private MatchRole multiplayerRole = MatchRole.Host;

    [Tooltip("Guest일 때 접속할 join code입니다. 연결 UI가 입력받은 값이 여기 채워집니다. " +
             "에디터에서 MPPM으로 띄웠으면 임시 파일로 자동 전달되는 값이 대신 쓰입니다.")]
    [SerializeField] private string multiplayerJoinCode;

    /// <summary>
    /// 연결 UI(StartMultiplayerAsHost/AsGuest)를 통해 역할·join code가 명시적으로 정해졌는지 여부입니다.
    /// true면 MatchSession이 MPPM 자동 배정보다 multiplayerRole/multiplayerJoinCode를 우선합니다.
    /// </summary>
    private bool explicitMultiplayerRole;

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

    /// <summary>
    /// 이번 매치의 초기 상태입니다. 원격 대전에서만 채워지며, 없으면 각자 뽑던 기존 방식대로 갑니다.
    /// GameManager가 매치 시작 시 읽어가므로 씬이 로드되기 전에 넣어야 합니다.
    /// </summary>
    public MatchSetup MatchSetup { get; private set; }

    /// <summary>합의된 초기 상태를 넣습니다. null을 넣으면 기존 방식(각자 뽑기)으로 돌아갑니다.</summary>
    public void SetMatchSetup(MatchSetup setup)
    {
        MatchSetup = setup;
        Debug.Log($"[Match] 초기 상태 지정: {(setup != null ? setup.ToString() : "없음")}");
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

        // 지난 매치의 초기 상태가 남아 있으면 새 매치가 엉뚱한 판으로 시작합니다.
        // 원격 대전에서는 핸드셰이크가 끝난 뒤 SetMatchSetup으로 다시 채웁니다.
        MatchSetup = null;

        PlayerState.Instance?.InitializeRun();

        // 원격 대전은 초기 상태 합의가 끝난 뒤에 맵을 엽니다.
        if (PrepareMatchSession())
            return;

        MapManager.Instance?.Init();
    }

    /// <summary>연결 UI의 "방 만들기"에서 호출합니다. 호스트로 멀티플레이 매치를 엽니다.</summary>
    public void StartMultiplayerAsHost()
    {
        StartMultiplayerMatch(MatchRole.Host, string.Empty);
    }

    /// <summary>연결 UI의 "코드로 참가"에서 호출합니다. 입력받은 join code로 게스트로 접속합니다.</summary>
    public void StartMultiplayerAsGuest(string joinCode)
    {
        StartMultiplayerMatch(MatchRole.Guest, joinCode);
    }

    private void StartMultiplayerMatch(MatchRole role, string joinCode)
    {
        CurrentMode = GameMode.Multiplayer;
        PlayerColor = DefaultPlayerColor;
        MatchSetup = null;

        multiplayerRole = role;
        multiplayerJoinCode = joinCode;
        explicitMultiplayerRole = true;

        PlayerState.Instance?.InitializeRun();

        if (PrepareMatchSession())
            return;

        MapManager.Instance?.Init();
    }

    /// <summary>연결 UI에서 취소했을 때 호출합니다. 세션을 정리하고 진입 상태로 되돌립니다.</summary>
    public void CancelMultiplayerConnect()
    {
        MatchSession.Instance?.EndMatch();

        explicitMultiplayerRole = false;
        multiplayerJoinCode = string.Empty;
        CurrentMode = GameMode.Run;
    }

    /// <summary>
    /// 모드에 맞춰 원격 연결을 준비합니다.
    ///
    /// 연결을 매치 씬이 아니라 여기서 여는 이유는, 게스트가 받을 초기 상태(MatchSetup)가
    /// 보드가 깔리기 전에 들어가 있어야 하기 때문입니다. 연결이 매치 씬 안에서 시작되면
    /// 그 시점에는 이미 늦습니다.
    /// </summary>
    /// <returns>합의를 기다려야 해서 진입을 미뤘으면 true</returns>
    private bool PrepareMatchSession()
    {
        if (CurrentMode != GameMode.Multiplayer)
        {
            // 지난 원격 대전의 연결이 남아 있으면 정리합니다.
            MatchSession.Instance?.EndMatch();
            return false;
        }

        MatchSession session = MatchSession.EnsureInstance();

        session.StateChanged -= OnMatchSessionStateChanged;
        session.StateChanged += OnMatchSessionStateChanged;

        session.Begin(new MatchSessionConfig
        {
            TransportKind = multiplayerTransport,
            LocalColor = PlayerColor,
            Role = multiplayerRole,
            JoinCode = multiplayerJoinCode,
            ExplicitRole = explicitMultiplayerRole
        });

        // 루프백은 합의할 대상이 없어 Begin 안에서 이미 Ready까지 갑니다.
        if (session.State == MatchSessionState.Ready)
        {
            session.StateChanged -= OnMatchSessionStateChanged;
            return false;
        }

        Debug.Log("[Match] 상대와 초기 상태를 맞추는 중입니다.");
        return true;
    }

    /// <summary>
    /// 합의가 끝나면 맵을 열고, 실패하면 진입을 취소합니다.
    ///
    /// 실패 표시는 최소한으로 갑니다. 전용 연결 UI는 매칭이 붙을 때의 몫입니다.
    /// </summary>
    private void OnMatchSessionStateChanged(MatchSessionState state)
    {
        MatchSession session = MatchSession.Instance;

        switch (state)
        {
            case MatchSessionState.Ready:
                if (session != null)
                    session.StateChanged -= OnMatchSessionStateChanged;

                // 진영은 세션이 합의 결과로 정해 넣어 줍니다.
                MapManager.Instance?.Init();

                // MapManager.Awake()가 씬 로드 시점에 이미 임시 맵을 만들어 뒀으므로(초기화 순서상
                // Init()보다 먼저 돎), 방금 다시 만든 진짜 맵으로 UI를 강제로 다시 짓는다.
                // MapUI가 핸드셰이크 중에는 노드를 비활성화해 뒀던 것도 여기서 함께 풀린다.
                MapUI.Instance?.Rebuild();
                break;

            case MatchSessionState.Failed:
                if (session != null)
                    session.StateChanged -= OnMatchSessionStateChanged;

                Debug.LogError($"[Match] 매치를 시작하지 못했습니다: {session?.FailureReason}");
                IngameToastUI.Instance?.Show($"매치 실패: {session?.FailureReason}");
                break;
        }
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
        PrepareMatchSession();
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
        PrepareMatchSession();
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
