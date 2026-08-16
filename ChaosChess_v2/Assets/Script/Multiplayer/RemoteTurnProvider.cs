using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상대 턴을 원격에서 받아 적용하는 프로바이더입니다.
///
/// 자기가 수를 계산하지 않고, 트랜스포트로 도착한 메시지를 보드에 반영하기만 합니다.
/// 지금은 검증용 LoopbackMatchTransport를 쓰고, 실제 전송이 붙으면 그 자리만 교체됩니다.
///
/// 씬에서 AiTurnController와 함께 두면 GameManager가 어느 쪽을 집을지 알 수 없으므로,
/// 둘 중 하나만 활성화해야 합니다.
/// </summary>
public sealed class RemoteTurnProvider : TurnProvider
{
    public override bool IsRemote => true;

    [Tooltip("어느 전송 계층으로 대전할지 고릅니다. Loopback은 네트워크 없이 흐름만 검증합니다.")]
    [SerializeField] private MatchTransportKind transportKind = MatchTransportKind.Loopback;

    [Header("Relay 검증용 (연결 UI가 붙으면 제거)")]
    [Tooltip("에디터에서는 MPPM 역할(메인=Host / 클론=Guest)이 우선합니다. 아래 값은 빌드용 폴백입니다.")]
    [SerializeField] private MatchRole relayRole = MatchRole.Host;

    [Tooltip("Guest일 때 호스트에게 받은 join code. 에디터에서는 임시 파일로 자동 전달됩니다.")]
    [SerializeField] private string relayJoinCode;

    [Tooltip("상대 카드를 적용한 뒤 뒤따르는 착수를 미룰 시간(초). 무슨 카드가 무엇을 했는지 볼 여유를 줍니다.")]
    [SerializeField] private float remoteCardPresentationSeconds = 1.2f;

    private IMatchTransport transport;

    // 게스트가 호스트의 join code가 나올 때까지 기다리는 중인지 여부입니다.
    private bool waitingForJoinCode;

    // 도착한 메시지를 바로 적용하지 않고 줄 세웁니다.
    // 카드와 착수는 한 턴에 연달아 오는데, 같은 프레임에 적용하면 판이 두 번 튀어 무슨 일이
    // 벌어졌는지 알아볼 수 없습니다. 순서는 큐가 그대로 보장합니다.
    private readonly Queue<MatchMessage> pendingMessages = new Queue<MatchMessage>();

    // 이 시각까지는 다음 메시지를 적용하지 않습니다.
    private float nextMessageAllowedAt;

    // 수신 시점에 중복을 거르는 기준입니다. 적용은 뒤로 미뤄지므로 lastAppliedSequence와 따로 둡니다.
    private int lastAcceptedSequence;

    // 턴 갱신을 기다리기 시작한 시각입니다. 기다릴 것이 없으면 -1입니다.
    private const float StallWarningSeconds = 10f;
    private float stalledSince = -1f;
    private bool stalledWarned;

    /// <summary>이번 인스턴스가 맡은 역할입니다. 에디터에서는 MPPM 판정이 인스펙터 값을 덮습니다.</summary>
    private MatchRole ResolvedRole =>
        MppmMatchRole.IsAvailable ? MppmMatchRole.Role : relayRole;

    // 상대에게서 마지막으로 반영한 일련번호. 중복·역순으로 도착한 메시지를 걸러냅니다.
    private int lastAppliedSequence;

    // 내가 보낸 행동에 매기는 일련번호입니다.
    private int localSequence;

    /// <summary>상대 행동을 기다리는 중인지 여부입니다. 대기 표시 UI가 참고합니다.</summary>
    public bool IsWaitingForRemote { get; private set; }

    /// <summary>대기 상태가 바뀔 때 발행됩니다.</summary>
    public event Action<bool> WaitingForRemoteChanged;

    private void SetWaitingForRemote(bool waiting)
    {
        if (IsWaitingForRemote == waiting) return;

        IsWaitingForRemote = waiting;
        WaitingForRemoteChanged?.Invoke(waiting);
    }

    private void Awake()
    {
        transport = CreateTransport(transportKind);
        transport.MessageReceived += HandleMessageReceived;
    }

    /// <summary>
    /// 호스트가 발급받은 join code입니다. 아직 없으면 빈 문자열입니다.
    /// 연결 UI가 붙기 전까지는 이 값을 Console 로그에서 확인해 게스트에게 전달합니다.
    /// </summary>
    public string HostJoinCode =>
        transport is RelayMatchTransport relay ? relay.HostJoinCode : string.Empty;

    private void Start()
    {
        // 지연 연결(TryRequestTurn 시점)은 루프백에서만 통합니다.
        // 실제 전송에서는 백을 잡은 쪽이 첫 수를 두기 전까지 방이 열리지 않아
        // 상대가 join code를 받을 수 없습니다. 그래서 매치 시작 시점에 미리 엽니다.
        if (transportKind == MatchTransportKind.Loopback) return;
        if (transport == null || transport.IsConnected) return;

        if (GameManager.Instance == null)
        {
            Debug.LogError("[Network] GameManager가 없어 매치를 열지 못했습니다.");
            return;
        }

        // 게스트는 호스트가 방을 열어야 join code를 알 수 있습니다.
        // 코드가 나올 때까지 Update에서 기다렸다가 접속합니다.
        if (ResolvedRole == MatchRole.Guest && MppmMatchRole.IsAvailable)
        {
            waitingForJoinCode = true;
            Debug.Log("[Network] 호스트가 방을 열기를 기다립니다.");
            return;
        }

        transport.StartMatch(GameManager.Instance.PlayerColor);
    }

    private void Update()
    {
        if (waitingForJoinCode)
            TryStartAsGuest();

        // UnityTransport처럼 프레임마다 수신을 꺼내야 하는 구현을 위해 돌려줍니다.
        // 먼저 받고 나서 적용해야 도착한 메시지가 같은 프레임에 반영됩니다.
        transport?.Tick();

        DrainPendingMessages();
    }

    /// <summary>줄 세운 메시지를 순서대로 적용합니다. 연출이나 턴 갱신이 진행 중이면 끝날 때까지 미룹니다.</summary>
    private void DrainPendingMessages()
    {
        while (pendingMessages.Count > 0)
        {
            if (Time.time < nextMessageAllowedAt)
                return;

            // 턴 전환의 합법수 조회가 끝나기 전에 적용하면 이전 턴 기준으로 판정해
            // 수가 조용히 버려집니다. 갱신이 끝날 때까지 기다립니다.
            if (GameManager.Instance != null && !GameManager.Instance.IsTurnStateReady)
            {
                WarnIfStalled();
                return;
            }

            stalledSince = -1f;
            ApplyMessage(pendingMessages.Dequeue());
        }

        stalledSince = -1f;
    }

    /// <summary>
    /// 턴 갱신을 기다리다 지나치게 오래 멈춰 있으면 알립니다.
    /// 엔진 콜백이 돌아오지 않으면 큐가 영영 비지 않으므로, 조용히 멈추는 대신 드러나게 합니다.
    /// </summary>
    private void WarnIfStalled()
    {
        if (stalledSince < 0f)
        {
            stalledSince = Time.time;
            return;
        }

        if (stalledWarned || Time.time - stalledSince < StallWarningSeconds)
            return;

        stalledWarned = true;
        Debug.LogError(
            $"[Remote] 턴 갱신을 {StallWarningSeconds}초 넘게 기다리고 있습니다. " +
            $"대기 중인 메시지 {pendingMessages.Count}개. 엔진 응답이 돌아오지 않았을 수 있습니다.");
    }

    /// <summary>호스트가 join code를 남겼으면 접속을 시작합니다. (MPPM 테스트 전용 경로)</summary>
    private void TryStartAsGuest()
    {
        if (GameManager.Instance == null) return;

        if (!MppmMatchRole.TryReadJoinCode(out string code))
            return;

        waitingForJoinCode = false;

        if (transport is RelayMatchTransport relay)
            relay.Configure(MatchRole.Guest, code);

        Debug.Log($"[Network] join code를 받아 접속합니다: {code}");
        transport.StartMatch(GameManager.Instance.PlayerColor);
    }

    private void OnDestroy()
    {
        if (transport == null) return;

        transport.MessageReceived -= HandleMessageReceived;
        transport.StopMatch();
    }

    /// <summary>
    /// 인스펙터에서 고른 종류로 전송 계층을 만듭니다.
    /// 게임 로직은 IMatchTransport만 알기 때문에 여기서 바꿔 끼우면 나머지는 그대로 갑니다.
    /// </summary>
    private IMatchTransport CreateTransport(MatchTransportKind kind)
    {
        switch (kind)
        {
            case MatchTransportKind.Relay:
                MatchRole role = ResolvedRole;
                Debug.Log($"[Network] Relay 역할: {role}" +
                          $"{(MppmMatchRole.IsAvailable ? " (MPPM 자동 배정)" : " (인스펙터 값)")}");

                // 지난 판에서 남은 join code를 게스트가 집어 가지 않도록 방을 열기 전에 지웁니다.
                if (role == MatchRole.Host && MppmMatchRole.IsAvailable)
                    MppmMatchRole.ClearJoinCode();

                RelayMatchTransport relay = new RelayMatchTransport();
                relay.Configure(role, relayJoinCode);

                relay.JoinCodeIssued += code =>
                {
                    Debug.Log($"[Network] 게스트에게 전달할 join code: {code}");
                    if (MppmMatchRole.IsAvailable)
                        MppmMatchRole.PublishJoinCode(code);
                };

                relay.ConnectionFailed += reason =>
                    Debug.LogError($"[Network] Relay 연결 실패: {reason}");

                return relay;

            default:
                return new LoopbackMatchTransport();
        }
    }

    public override bool TryRequestTurn(
        GameManager gameManager,
        BoardManager boardManager,
        Action fallbackMoveRequest)
    {
        if (gameManager == null || boardManager == null)
            return false;

        // GameManager보다 먼저 초기화됐을 수 있으므로 첫 요청 시점에 매치를 엽니다.
        if (!transport.IsConnected)
            transport.StartMatch(gameManager.PlayerColor);

        // 상대 응답이 도착할 때까지 기다립니다. 기본 착수 경로로 넘어가면 안 되므로 true를 돌려줍니다.
        SetWaitingForRemote(true);
        transport.NotifyRemoteTurnStarted(gameManager.CurrentTurn);
        return true;
    }

    public override void SendLocalAction(MatchMessage message)
    {
        if (transport == null || message == null) return;

        message.Sequence = ++localSequence;
        transport.Send(message);
    }

    private void HandleMessageReceived(MatchMessage message)
    {
        if (message == null) return;

        // 늦게 도착했거나 이미 받은 행동이면 버립니다. 네트워크가 붙으면 실제로 발생합니다.
        if (message.Sequence <= lastAcceptedSequence)
        {
            Debug.LogWarning($"[Remote] 순서가 지난 메시지를 버립니다. {message} (마지막 수신 #{lastAcceptedSequence})");
            return;
        }

        lastAcceptedSequence = message.Sequence;
        pendingMessages.Enqueue(message);
    }

    /// <summary>줄에서 꺼낸 메시지 하나를 실제로 보드에 반영합니다.</summary>
    private void ApplyMessage(MatchMessage message)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.IsEndGame) return;
        if (BoardManager.Instance == null) return;

        PieceColor remoteColor = CardTargetRelationExtensions.Opposite(gameManager.PlayerColor);

        // 카드는 같은 턴에 착수가 뒤따르므로, 착수를 받을 때만 대기를 해제합니다.
        if (message.Kind != MatchMessageKind.Card)
            SetWaitingForRemote(false);

        switch (message.Kind)
        {
            case MatchMessageKind.Move:
                ApplyRemoteMove(message);
                break;

            case MatchMessageKind.Resign:
                lastAppliedSequence = message.Sequence;
                gameManager.OnSurrender(remoteColor);
                break;

            case MatchMessageKind.Card:
                lastAppliedSequence = message.Sequence;

                // 카드가 실제로 적용됐을 때만 뒤따르는 착수를 미룹니다.
                // 카드를 못 찾아 실패한 경우까지 기다리면 대국만 늘어집니다.
                if (RemoteCardExecutor.TryExecute(message, remoteColor))
                    nextMessageAllowedAt = Time.time + remoteCardPresentationSeconds;
                break;
        }
    }

    private void ApplyRemoteMove(MatchMessage message)
    {
        if (!BoardManager.Instance.IsValidUciMove(message.Uci))
        {
            Debug.LogError($"[Remote] 잘못된 착수를 받았습니다: '{message.Uci}'");
            return;
        }

        lastAppliedSequence = message.Sequence;

        // ApplyUCIMove가 이동 연출 후 NextTurn까지 이어받으므로 턴 진행을 따로 부르지 않습니다.
        BoardManager.Instance.ApplyUCIMove(message.Uci);
    }
}
