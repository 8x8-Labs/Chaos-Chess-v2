using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상대 턴을 원격에서 받아 적용하는 프로바이더입니다.
///
/// 자기가 수를 계산하지 않고, 트랜스포트로 도착한 메시지를 보드에 반영하기만 합니다.
///
/// 연결은 이 프로바이더가 소유하지 않습니다. 매치 씬보다 오래 살아야 하는 자원이라
/// MatchSession이 들고 있고, 여기서는 세션에서 통로를 빌려 구독만 합니다.
///
/// 씬에서 AiTurnController와 함께 두면 GameManager가 어느 쪽을 집을지 알 수 없으므로,
/// 둘 중 하나만 활성화해야 합니다.
/// </summary>
public sealed class RemoteTurnProvider : TurnProvider
{
    public override bool IsRemote => true;

    [Tooltip("상대 카드를 적용한 뒤 뒤따르는 착수를 미룰 시간(초). 무슨 카드가 무엇을 했는지 볼 여유를 줍니다.")]
    [SerializeField] private float remoteCardPresentationSeconds = 1.2f;

    /// <summary>세션이 들고 있는 전송 계층입니다. 세션이 없으면 null입니다.</summary>
    private IMatchTransport Transport => MatchSession.Instance?.Transport;

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

    // 상대에게서 마지막으로 반영한 일련번호. 중복·역순으로 도착한 메시지를 걸러냅니다.
    private int lastAppliedSequence;

    // 내가 보낸 행동에 매기는 일련번호입니다.
    private int localSequence;

    // 세션에 구독자로 붙은 적이 있는지 여부입니다. AI 대전에서는 끝까지 false로 남습니다.
    private bool sessionJoined;

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

    private void OnEnable()
    {
        // 연결은 MainScene에서 이미 열려 있습니다. 여기서는 받을 사람으로 등록만 합니다.
        // 씬 로드 중에 도착해 세션이 버퍼에 담아 둔 메시지도 이 시점에 함께 넘어옵니다.
        if (MatchSession.Instance == null)
        {
            // 멀티 모드가 아니면 세션이 없는 것이 정상입니다. 이 프로바이더는 씬에 남아 있어도
            // GameManager가 고르지 않으므로 조용히 놀립니다.
            if (GameCycleManager.Instance != null &&
                GameCycleManager.Instance.CurrentMode == GameMode.Multiplayer)
            {
                Debug.LogError("[Remote] 멀티플레이 모드인데 매치 세션이 열려 있지 않습니다.");
            }

            return;
        }

        MatchSession.Instance.Subscribe(HandleMessageReceived);
        sessionJoined = true;

        // 연결이 안 된 채로 대국에 들어가면 첫 수를 둘 때가 되어서야 드러납니다.
        // 원인에서 멀어지므로 씬에 들어오는 시점에 먼저 알립니다.
        if (Transport == null || !Transport.IsConnected)
        {
            Debug.LogError(
                $"[Remote] 상대와 연결되지 않은 채 매치에 들어왔습니다. (세션 상태: {MatchSession.Instance.State}) " +
                "이대로면 착수를 보낼 수 없습니다.");
        }
    }

    private void OnDisable()
    {
        if (!sessionJoined) return;

        MatchSession.Instance?.Unsubscribe(HandleMessageReceived);
    }

    private void Update()
    {
        // Tick() 펌핑은 세션이 맡습니다. 매치 씬 밖에서도 수신이 멈추면 안 되기 때문입니다.
        // 세션은 [DefaultExecutionOrder(-100)]으로 먼저 돌므로 이번 프레임에 도착한 메시지가
        // 여기서 곧바로 적용됩니다.
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

    private void OnDestroy()
    {
        // 매치 씬을 떠나면 연결도 끝냅니다. 재접속 처리는 4단계의 몫입니다.
        // 세션에 붙은 적이 없으면(= AI 대전이면) 남의 연결을 끊지 않도록 건너뜁니다.
        if (!sessionJoined) return;

        MatchSession.Instance?.EndMatch();
    }

    public override bool TryRequestTurn(
        GameManager gameManager,
        BoardManager boardManager,
        Action fallbackMoveRequest)
    {
        if (gameManager == null || boardManager == null)
            return false;

        // 상대 응답이 도착할 때까지 기다립니다. 기본 착수 경로로 넘어가면 안 되므로 true를 돌려줍니다.
        SetWaitingForRemote(true);

        IMatchTransport current = Transport;
        if (current == null)
        {
            // false를 돌려주면 로컬 엔진이 상대 대신 수를 둬 판이 조용히 갈립니다.
            // 진행을 멈추고 로그로 드러내는 편이 낫습니다.
            Debug.LogError("[Remote] 매치 세션이 열려 있지 않아 상대 턴을 기다릴 수 없습니다.");
            return true;
        }

        current.NotifyRemoteTurnStarted(gameManager.CurrentTurn);
        return true;
    }

    public override void SendLocalAction(MatchMessage message)
    {
        if (message == null) return;

        IMatchTransport current = Transport;
        if (current == null)
        {
            Debug.LogError($"[Remote] 매치 세션이 없어 전송하지 못했습니다: {message}");
            return;
        }

        message.Sequence = ++localSequence;
        current.Send(message);
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
