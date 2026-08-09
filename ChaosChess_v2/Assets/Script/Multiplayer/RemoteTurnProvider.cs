using System;
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
    private IMatchTransport transport;

    // 이미 반영한 턴 번호. 중복·역순으로 도착한 메시지를 걸러냅니다.
    private int lastAppliedTurn = -1;

    private void Awake()
    {
        transport = new LoopbackMatchTransport();
        transport.MessageReceived += HandleMessageReceived;
    }

    private void OnDestroy()
    {
        if (transport == null) return;

        transport.MessageReceived -= HandleMessageReceived;
        transport.StopMatch();
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
        transport.NotifyRemoteTurnStarted(gameManager.CurrentTurn);
        return true;
    }

    public override void SendLocalAction(MatchMessage message)
    {
        if (transport == null || message == null) return;

        transport.Send(message);
    }

    private void HandleMessageReceived(MatchMessage message)
    {
        if (message == null) return;

        // 늦게 도착했거나 이미 반영한 턴이면 버립니다. 네트워크가 붙으면 실제로 발생합니다.
        if (message.Turn <= lastAppliedTurn)
        {
            Debug.LogWarning($"[Remote] 순서가 지난 메시지를 버립니다. {message} (마지막 반영 턴 {lastAppliedTurn})");
            return;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.IsEndGame) return;
        if (BoardManager.Instance == null) return;

        switch (message.Kind)
        {
            case MatchMessageKind.Move:
                ApplyRemoteMove(message);
                break;

            case MatchMessageKind.Resign:
                lastAppliedTurn = message.Turn;
                gameManager.OnSurrender(CardTargetRelationExtensions.Opposite(gameManager.PlayerColor));
                break;

            case MatchMessageKind.Card:
                // 카드 동기화는 다음 단계에서 붙입니다.
                Debug.Log($"[Remote] 카드 메시지는 아직 처리하지 않습니다. {message}");
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

        lastAppliedTurn = message.Turn;

        // ApplyUCIMove가 이동 연출 후 NextTurn까지 이어받으므로 턴 진행을 따로 부르지 않습니다.
        BoardManager.Instance.ApplyUCIMove(message.Uci);
    }
}
