using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>매치 연결의 진행 단계입니다.</summary>
public enum MatchSessionState
{
    Idle,

    /// <summary>트랜스포트를 열고 상대와 이어지기를 기다리는 중입니다.</summary>
    Connecting,

    /// <summary>연결은 됐고 초기 상태를 합의하는 중입니다. (3b에서 실제로 쓰입니다)</summary>
    Handshaking,

    /// <summary>대국을 시작해도 되는 상태입니다.</summary>
    Ready,

    Failed
}

/// <summary>매치를 열 때 필요한 설정입니다. MainScene의 GameCycleManager가 채워 넘깁니다.</summary>
public struct MatchSessionConfig
{
    /// <summary>어느 전송 계층으로 대전할지입니다.</summary>
    public MatchTransportKind TransportKind;

    /// <summary>이 클라이언트가 맡은 진영입니다.</summary>
    public PieceColor LocalColor;

    /// <summary>에디터 밖(빌드)에서 쓸 역할입니다. 에디터에서는 MPPM 판정이 우선합니다.</summary>
    public MatchRole FallbackRole;

    /// <summary>에디터 밖에서 게스트가 쓸 join code입니다.</summary>
    public string FallbackJoinCode;
}

/// <summary>
/// 원격 대전의 연결을 소유하는 세션입니다.
///
/// 연결의 수명은 매치 씬보다 길어야 합니다. 게스트가 받은 초기 상태(MatchSetup)는
/// 보드가 깔리기 전에 GameCycleManager에 들어가 있어야 하는데, 연결이 매치 씬 안에서
/// 시작되면 그 시점에는 이미 늦기 때문입니다. 그래서 트랜스포트를 씬 오브젝트가 아니라
/// DontDestroyOnLoad 오브젝트인 이 세션이 들고 있습니다.
///
/// 책임은 셋입니다.
///   1. 트랜스포트 생성과 연결 시작 (MainScene에서)
///   2. 매 프레임 Tick() 펌핑 — 매치 씬 밖에서도 수신이 멈추지 않아야 합니다
///   3. 구독자가 없는 동안 도착한 메시지 버퍼링 — 씬 로드 중에는 RemoteTurnProvider가 없습니다
///
/// 메시지를 실제로 보드에 반영하는 일은 여전히 RemoteTurnProvider의 몫입니다.
/// 세션은 통로만 빌려줍니다.
///
/// 실행 순서를 앞으로 당겨 둔 이유: 수신(Tick)이 적용(RemoteTurnProvider.Update)보다 먼저
/// 돌아야 도착한 메시지가 같은 프레임에 반영됩니다. 순서가 뒤집히면 한 프레임씩 밀립니다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class MatchSession : MonoBehaviour
{
    public static MatchSession Instance { get; private set; }

    private IMatchTransport transport;
    private MatchSessionConfig config;

    /// <summary>이번 매치의 전송 계층입니다. 세션이 열려 있지 않으면 null입니다.</summary>
    public IMatchTransport Transport => transport;

    public MatchSessionState State { get; private set; } = MatchSessionState.Idle;

    /// <summary>Failed 상태일 때의 사유입니다. 표시는 3b의 실패 처리에서 붙입니다.</summary>
    public string FailureReason { get; private set; }

    /// <summary>상태가 바뀔 때 발행됩니다.</summary>
    public event Action<MatchSessionState> StateChanged;

    /// <summary>호스트가 발급받은 join code입니다. 없으면 빈 문자열입니다.</summary>
    public string HostJoinCode =>
        transport is RelayMatchTransport relay ? relay.HostJoinCode : string.Empty;

    // 행동 메시지를 받을 단 하나의 구독자입니다. 매치 씬의 RemoteTurnProvider가 붙습니다.
    private Action<MatchMessage> messageHandler;

    // 구독자가 없는 동안 도착한 메시지를 담아 둡니다.
    //
    // 씬 로드 중에는 RemoteTurnProvider가 아직 없습니다. 호스트가 백이면 게스트보다 먼저
    // 첫 수를 둘 수 있고, 그 메시지가 게스트의 씬 로드 중에 도착하면 받을 사람이 없습니다.
    // 여기 담아 두었다가 구독 시점에 순서대로 흘려줍니다.
    private readonly Queue<MatchMessage> bufferedMessages = new Queue<MatchMessage>();

    // 게스트가 호스트의 join code가 나올 때까지 기다리는 중인지 여부입니다.
    private bool waitingForJoinCode;

    // 프레임이 이 시간 이상 끊기면 Relay 핑이 밀리기 시작합니다.
    private const float TickStallWarningSeconds = 2f;

    // 마지막으로 드라이버를 돌린 시각입니다. 돌릴 것이 없으면 -1입니다.
    private float lastTickAt = -1f;

    // 핸드셰이크가 이 시간을 넘기면 실패로 넘깁니다. 연결 성립·Setup 수신·Ack 수신 전 구간에 걸립니다.
    private const float HandshakeTimeoutSeconds = 15f;
    private float handshakeStartedAt = -1f;

    // 호스트가 자기 몫으로 쥐고 있는 초기 상태입니다. Ack를 받으면 이 값을 적용합니다.
    private MatchSetup pendingLocalSetup;

    // 거부 Ack를 보낸 직후의 실패는 몇 틱 미룹니다. 아래 DeferFail 참고.
    private string deferredFailReason;
    private int deferredFailTicks;

    /// <summary>상대의 표시용 프로필입니다. 핸드셰이크에서 받아 둡니다.</summary>
    public MatchProfile RemoteProfile { get; private set; }

    // 게스트가 join code를 이만큼 기다렸는데도 못 받으면 알립니다.
    private const float JoinCodeWaitWarningSeconds = 20f;
    private float joinCodeWaitStartedAt = -1f;
    private bool joinCodeWaitWarned;

    // 세션이 runInBackground를 켜 두었는지와, 켜기 전 값입니다.
    private bool runInBackgroundOverridden;
    private bool previousRunInBackground;

    /// <summary>이번 인스턴스가 맡은 역할입니다. 에디터에서는 MPPM 판정이 설정값을 덮습니다.</summary>
    private MatchRole ResolvedRole =>
        MppmMatchRole.IsAvailable ? MppmMatchRole.Role : config.FallbackRole;

    /// <summary>세션 오브젝트를 보장합니다. 없으면 만들어 씬을 넘어 살아남게 합니다.</summary>
    public static MatchSession EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject host = new GameObject(nameof(MatchSession));
        DontDestroyOnLoad(host);
        return host.AddComponent<MatchSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        EndMatch();
        Instance = null;
    }

    // ── 수명 ────────────────────────────────────────────

    /// <summary>
    /// 매치를 엽니다. 이미 열려 있던 연결은 먼저 정리합니다.
    /// 매치 씬에 들어가기 전에 MainScene에서 불러야 합니다.
    /// </summary>
    public void Begin(MatchSessionConfig sessionConfig)
    {
        EndMatch();

        KeepRunningWithoutFocus();
        WarmUpRuleOracle();

        config = sessionConfig;
        transport = CreateTransport(config.TransportKind);
        transport.MessageReceived += HandleTransportMessage;
        transport.ControlReceived += HandleControlMessage;
        transport.Connected += HandleConnected;

        SetState(MatchSessionState.Connecting);

        // 게스트는 호스트가 방을 열어야 join code를 알 수 있습니다.
        // 코드가 나올 때까지 Update에서 기다렸다가 접속합니다.
        if (config.TransportKind == MatchTransportKind.Relay &&
            ResolvedRole == MatchRole.Guest &&
            MppmMatchRole.IsAvailable)
        {
            waitingForJoinCode = true;
            joinCodeWaitStartedAt = Time.realtimeSinceStartup;
            Debug.Log("[Session] 호스트가 방을 열기를 기다립니다.");
            return;
        }

        transport.StartMatch(config.LocalColor);
    }

    /// <summary>
    /// 창이 포커스를 잃어도 루프를 계속 돌리게 합니다.
    ///
    /// 포커스가 없으면 Unity가 플레이어 루프를 멈추는데, 드라이버는 Update에서만 도므로
    /// 그동안 Relay 핑도 함께 멈춥니다. Relay는 조용한 쪽을 끊으므로 상대 창을 만지는 사이에
    /// 연결이 죽습니다. MPPM 테스트는 두 창을 번갈아 조작해야 하니 반드시 필요합니다.
    ///
    /// **프로젝트 설정(Player → Run In Background)은 건드리지 않습니다.** 그 값은 플랫폼
    /// 공통이라 Android 빌드까지 따라가고, 모바일에서 백그라운드로 계속 도는 것은 배터리를
    /// 낭비합니다. 그래서 원격 대전 세션이 열려 있는 동안만 켜고 끝나면 되돌립니다.
    /// </summary>
    private void KeepRunningWithoutFocus()
    {
        if (runInBackgroundOverridden) return;

        previousRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        runInBackgroundOverridden = true;
    }

    /// <summary>매치가 끝나면 원래 설정으로 돌려놓습니다.</summary>
    private void RestoreRunInBackground()
    {
        if (!runInBackgroundOverridden) return;

        Application.runInBackground = previousRunInBackground;
        runInBackgroundOverridden = false;
    }

    /// <summary>
    /// 연결을 열기 전에 룰 오라클(Fairy Stockfish)을 미리 깨웁니다.
    ///
    /// 엔진 기동은 프로세스를 띄우고 readyok를 기다리는 동안 **메인 스레드를 수 초간 멈춥니다.**
    /// 그 일이 연결이 열린 뒤에 벌어지면 그동안 드라이버가 돌지 못하고, Relay는 조용한 쪽을
    /// 비활성으로 보고 할당을 무효화합니다("player timed out due to inactivity").
    /// 매치 씬의 InitEngine 호출은 같은 변형이면 재사용으로 떨어져 그때는 멈추지 않습니다.
    ///
    /// 씬에 브리지가 없으면 만들어 둡니다. 매치 씬에 있는 브리지는 싱글턴 규칙에 따라
    /// 스스로 물러나고, 코드가 브리지를 참조하는 경로는 Instance 하나뿐이라 안전합니다.
    /// </summary>
    private static void WarmUpRuleOracle()
    {
        FairyStockfishBridge bridge = FairyStockfishBridge.Instance;

        if (bridge == null)
        {
            GameObject host = new GameObject(nameof(FairyStockfishBridge));
            bridge = host.AddComponent<FairyStockfishBridge>();
        }

        // GameManager.LoadMapManager()가 넘기는 변형과 반드시 같아야 합니다.
        // 다르면 매치 씬에서 엔진을 다시 띄우게 되어 미리 깨운 의미가 없습니다.
        bridge.InitEngine("chaoschess");
    }

    /// <summary>매치를 끝내고 연결을 정리합니다. 열려 있지 않으면 아무것도 하지 않습니다.</summary>
    public void EndMatch()
    {
        DisposeTransport();
        ResetSessionState();
    }

    /// <summary>연결만 정리합니다. 세션 상태는 건드리지 않습니다.</summary>
    private void DisposeTransport()
    {
        if (transport == null) return;

        transport.MessageReceived -= HandleTransportMessage;
        transport.ControlReceived -= HandleControlMessage;
        transport.Connected -= HandleConnected;
        transport.StopMatch();
        transport = null;
    }

    private void ResetSessionState()
    {
        waitingForJoinCode = false;
        joinCodeWaitStartedAt = -1f;
        joinCodeWaitWarned = false;
        bufferedMessages.Clear();
        messageHandler = null;
        FailureReason = null;
        lastTickAt = -1f;
        handshakeStartedAt = -1f;
        pendingLocalSetup = null;
        RemoteProfile = null;
        deferredFailReason = null;

        RestoreRunInBackground();

        SetState(MatchSessionState.Idle);
    }

    private void SetState(MatchSessionState next)
    {
        if (State == next) return;

        State = next;
        StateChanged?.Invoke(next);
    }

    /// <summary>
    /// 매치를 열지 못했음을 확정합니다.
    ///
    /// 연결은 정리하되 상태는 Failed로 남깁니다. 사유를 화면에 보여줘야 하므로
    /// EndMatch처럼 Idle로 되돌리면 안 됩니다.
    /// </summary>
    private void Fail(string reason)
    {
        Debug.LogError($"[Session] {reason}");

        DisposeTransport();
        FailureReason = reason;
        SetState(MatchSessionState.Failed);
    }

    // ── 구독 ────────────────────────────────────────────

    /// <summary>
    /// 행동 메시지를 받을 구독자를 붙입니다. 구독자는 하나뿐이며, 붙는 즉시
    /// 그동안 버퍼에 쌓인 메시지를 순서대로 넘겨받습니다.
    /// </summary>
    public void Subscribe(Action<MatchMessage> handler)
    {
        if (handler == null) return;

        messageHandler = handler;

        if (bufferedMessages.Count == 0) return;

        Debug.Log($"[Session] 버퍼에 있던 메시지 {bufferedMessages.Count}개를 전달합니다.");
        while (bufferedMessages.Count > 0)
            handler(bufferedMessages.Dequeue());
    }

    /// <summary>구독을 뗍니다. 이후 도착하는 메시지는 다시 버퍼에 쌓입니다.</summary>
    public void Unsubscribe(Action<MatchMessage> handler)
    {
        if (messageHandler == handler)
            messageHandler = null;
    }

    /// <summary>
    /// 상대와 이어졌습니다. 여기가 핸드셰이크를 시작할 자리입니다.
    ///
    /// 아직 3b의 Setup/SetupAck 왕복이 없으므로 곧바로 Ready로 갑니다.
    /// 루프백은 합의할 대상이 없어 앞으로도 이 경로 그대로입니다.
    /// </summary>
    private void HandleConnected()
    {
        if (State != MatchSessionState.Connecting) return;

        // 루프백은 상대가 로컬 엔진이라 합의할 대상이 없습니다.
        // MatchSetup을 null로 남겨 두면 기존 단일 플레이 경로로 떨어집니다.
        if (config.TransportKind != MatchTransportKind.Relay)
        {
            SetState(MatchSessionState.Ready);
            return;
        }

        SetState(MatchSessionState.Handshaking);
        handshakeStartedAt = Time.realtimeSinceStartup;

        // 게스트는 호스트가 보낼 Setup을 기다립니다.
        if (ResolvedRole != MatchRole.Host)
            return;

        MatchSetupFactory.Build(out MatchSetup hostSetup, out MatchSetup guestSetup);

        // 호스트 몫은 여기서 바로 쥐고, 게스트 몫만 보냅니다.
        // 상대 몫까지 실어 보내면 받는 쪽이 내 손패를 미리 알게 됩니다(설계문서 5절).
        pendingLocalSetup = hostSetup;
        transport.SendControl(
            MatchControlMessage.CreateSetup(guestSetup, MatchProfile.CreateLocal(MatchRole.Host)));
    }

    private void HandleControlMessage(MatchControlMessage message)
    {
        if (message == null) return;

        if (State != MatchSessionState.Handshaking)
        {
            Debug.LogWarning($"[Session] 핸드셰이크 중이 아닌데 제어 메시지가 왔습니다({State}): {message}");
            return;
        }

        switch (message.Kind)
        {
            case MatchControlKind.Setup:
                HandleSetup(message);
                break;

            case MatchControlKind.SetupAck:
                HandleSetupAck(message);
                break;
        }
    }

    /// <summary>게스트가 호스트의 초기 상태를 받아 검증하고 답합니다.</summary>
    private void HandleSetup(MatchControlMessage message)
    {
        if (ResolvedRole == MatchRole.Host)
        {
            Debug.LogWarning("[Session] 호스트가 Setup을 받았습니다. 무시합니다.");
            return;
        }

        RemoteProfile = message.Profile;

        MatchSetup setup = message.Setup;
        string reason = "초기 상태가 비어 있습니다.";
        bool valid = setup != null && setup.TryValidate(out reason);

        transport.SendControl(
            MatchControlMessage.CreateAck(valid, reason, MatchProfile.CreateLocal(MatchRole.Guest)));

        if (!valid)
        {
            DeferFail($"상대가 보낸 초기 상태를 받아들일 수 없습니다. {reason}");
            return;
        }

        ApplyAgreedSetup(setup);
    }

    /// <summary>
    /// 호스트가 게스트의 응답을 받습니다.
    ///
    /// Ack를 기다리는 이유는 불일치를 감지할 수 있는 쪽이 게스트뿐이기 때문입니다.
    /// 기다리지 않으면 호스트만 대국 화면에 들어가 오지 않을 상대를 영영 기다립니다.
    /// </summary>
    private void HandleSetupAck(MatchControlMessage message)
    {
        if (ResolvedRole != MatchRole.Host)
        {
            Debug.LogWarning("[Session] 게스트가 SetupAck을 받았습니다. 무시합니다.");
            return;
        }

        RemoteProfile = message.Profile;

        if (!message.Accepted)
        {
            Fail($"상대가 초기 상태를 거부했습니다. {message.Reason}");
            return;
        }

        if (pendingLocalSetup == null)
        {
            Fail("보낸 초기 상태를 잃어버려 매치를 시작할 수 없습니다.");
            return;
        }

        ApplyAgreedSetup(pendingLocalSetup);
    }

    /// <summary>
    /// 합의가 끝났습니다. 매치 씬이 열리기 전에 초기 상태와 진영을 넣어 둡니다.
    ///
    /// 진영은 지금 역할에서 파생시킵니다(호스트=백). 서버가 색을 배정하는 5단계에서는
    /// 이 한 줄만 갈아끼우면 됩니다.
    /// </summary>
    private void ApplyAgreedSetup(MatchSetup setup)
    {
        pendingLocalSetup = null;

        GameCycleManager cycle = GameCycleManager.Instance;
        if (cycle == null)
        {
            Fail("GameCycleManager를 찾지 못했습니다.");
            return;
        }

        cycle.SetMatchSetup(setup);
        cycle.SetPlayerColor(ResolvedRole == MatchRole.Host ? PieceColor.White : PieceColor.Black);

        Debug.Log($"[Session] 초기 상태 합의 완료. 상대: {RemoteProfile}");
        SetState(MatchSessionState.Ready);
    }

    /// <summary>
    /// 실패를 몇 틱 미룹니다.
    ///
    /// 방금 넣은 제어 메시지는 다음 드라이버 갱신에서야 실제로 나갑니다. 곧바로 Fail하면
    /// StopMatch가 드라이버를 Dispose해 <b>거부 Ack가 상대에게 도착하지 못하고</b>,
    /// 상대는 사유 대신 15초 타임아웃만 보게 됩니다.
    /// </summary>
    private void DeferFail(string reason)
    {
        deferredFailReason = reason;
        deferredFailTicks = 2;
    }

    private void ProcessDeferredFail()
    {
        if (deferredFailReason == null) return;
        if (--deferredFailTicks > 0) return;

        string reason = deferredFailReason;
        deferredFailReason = null;
        Fail(reason);
    }

    /// <summary>핸드셰이크가 시간 안에 끝나지 않으면 실패로 넘깁니다.</summary>
    private void CheckHandshakeTimeout()
    {
        if (State != MatchSessionState.Handshaking) return;
        if (Time.realtimeSinceStartup - handshakeStartedAt < HandshakeTimeoutSeconds) return;

        Fail($"상대 응답이 {HandshakeTimeoutSeconds}초 안에 오지 않았습니다.");
    }

    private void HandleTransportMessage(MatchMessage message)
    {
        if (message == null) return;

        if (messageHandler != null)
        {
            messageHandler(message);
            return;
        }

        bufferedMessages.Enqueue(message);
        Debug.Log($"[Session] 받을 사람이 아직 없어 버퍼에 담습니다: {message}");
    }

    // ── 프레임 ──────────────────────────────────────────

    private void Update()
    {
        if (waitingForJoinCode)
            TryStartAsGuest();

        WarnIfMainThreadStalled();

        // 매치 씬 밖에서도 수신이 멈추면 안 되므로 세션이 펌프를 돌립니다.
        // Relay 연결도 이 Tick 안에서 성립하고, 그 순간 Connected 이벤트가 올라옵니다.
        transport?.Tick();

        ProcessDeferredFail();
        CheckHandshakeTimeout();
    }

    /// <summary>
    /// 프레임이 오래 끊겼으면 알립니다.
    ///
    /// 드라이버는 여기서만 돌기 때문에, 메인 스레드가 멈추면 Relay 핑도 함께 멈춥니다.
    /// Relay는 조용한 쪽을 비활성으로 보고 끊어 버리므로, 원인 모를 연결 끊김 대신
    /// 무엇이 멈췄는지 먼저 드러나게 합니다.
    /// </summary>
    private void WarnIfMainThreadStalled()
    {
        if (transport == null)
        {
            lastTickAt = -1f;
            return;
        }

        float now = Time.realtimeSinceStartup;

        if (lastTickAt >= 0f)
        {
            float gap = now - lastTickAt;
            if (gap >= TickStallWarningSeconds)
            {
                Debug.LogError(
                    $"[Session] 메인 스레드가 {gap:F1}초 멈춰 있었습니다. " +
                    $"(씬: {SceneManager.GetActiveScene().name}, 상태: {State}) " +
                    "그동안 Relay 핑이 나가지 못했습니다. 이 시간이 길어지면 " +
                    "\"player timed out due to inactivity\"로 연결이 끊깁니다.");
            }
        }

        lastTickAt = now;
    }

    /// <summary>
    /// 게스트가 join code를 너무 오래 기다리고 있으면 알립니다.
    ///
    /// 조용히 기다리기만 하면 연결되지 않은 채 대국에 들어가고, 첫 수를 둘 때가 되어서야
    /// "연결되지 않아 전송하지 못했습니다"로 드러납니다. 원인에서 멀어지므로 여기서 먼저 알립니다.
    /// </summary>
    private void WarnIfJoinCodeWaitTooLong()
    {
        if (joinCodeWaitWarned || joinCodeWaitStartedAt < 0f) return;
        if (Time.realtimeSinceStartup - joinCodeWaitStartedAt < JoinCodeWaitWarningSeconds) return;

        joinCodeWaitWarned = true;
        Debug.LogError(
            $"[Session] join code를 {JoinCodeWaitWarningSeconds}초 넘게 기다리고 있습니다. " +
            "호스트가 아직 게임을 시작하지 않았거나, 호스트가 남긴 파일을 읽지 못하고 있습니다.");
    }

    /// <summary>호스트가 join code를 남겼으면 접속을 시작합니다. (MPPM 테스트 전용 경로)</summary>
    private void TryStartAsGuest()
    {
        if (!MppmMatchRole.TryReadJoinCode(out string code))
        {
            WarnIfJoinCodeWaitTooLong();
            return;
        }

        waitingForJoinCode = false;
        joinCodeWaitWarned = false;

        if (transport is RelayMatchTransport relay)
            relay.Configure(MatchRole.Guest, code);

        Debug.Log($"[Session] join code를 받아 접속합니다: {code}");
        transport.StartMatch(config.LocalColor);
    }

    // ── 트랜스포트 생성 ─────────────────────────────────

    /// <summary>
    /// 설정에서 고른 종류로 전송 계층을 만듭니다.
    /// 게임 로직은 IMatchTransport만 알기 때문에 여기서 바꿔 끼우면 나머지는 그대로 갑니다.
    /// </summary>
    private IMatchTransport CreateTransport(MatchTransportKind kind)
    {
        switch (kind)
        {
            case MatchTransportKind.Relay:
                MatchRole role = ResolvedRole;
                Debug.Log($"[Session] Relay 역할: {role}" +
                          $"{(MppmMatchRole.IsAvailable ? " (MPPM 자동 배정)" : " (설정 값)")}");

                // 지난 판에서 남은 join code를 게스트가 집어 가지 않도록 방을 열기 전에 지웁니다.
                if (role == MatchRole.Host && MppmMatchRole.IsAvailable)
                    MppmMatchRole.ClearJoinCode();

                RelayMatchTransport relay = new RelayMatchTransport();
                relay.Configure(role, config.FallbackJoinCode);

                relay.JoinCodeIssued += code =>
                {
                    Debug.Log($"[Session] 게스트에게 전달할 join code: {code}");
                    if (MppmMatchRole.IsAvailable)
                        MppmMatchRole.PublishJoinCode(code);
                };

                relay.ConnectionFailed += reason => Fail($"Relay 연결 실패: {reason}");

                return relay;

            default:
                return new LoopbackMatchTransport();
        }
    }
}
