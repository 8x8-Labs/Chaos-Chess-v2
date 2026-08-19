using System;
using UnityEngine;

/// <summary>
/// 네트워크 없이 매치 흐름을 검증하기 위한 트랜스포트입니다.
///
/// 보낸 메시지는 기록만 하고, 상대 차례가 되면 Fairy Stockfish에게 수를 물어
/// 원격에서 도착한 것처럼 MessageReceived로 발행합니다.
/// 겉보기에는 원격 대전이지만 실제로는 로컬 엔진이 상대역을 맡습니다.
///
/// 목적은 전송 계층·메시지 스키마·수신 주입 경로를 실제와 같은 코드로 검증하는 것입니다.
/// 실제 전송(UGS Relay)으로 갈아끼울 때 게임 로직은 손대지 않습니다.
/// </summary>
public sealed class LoopbackMatchTransport : IMatchTransport
{
    private const int AnalysisDepth = 12;
    private const int MoveTimeMs = 2000;

    private PieceColor localColor;
    private bool running;

    // 같은 턴에 대해 중복으로 수를 요청하지 않도록 진행 중인 턴을 기억합니다.
    private int requestedTurn = -1;

    // 상대가 보낸 것처럼 꾸미기 위해 이쪽에서도 일련번호를 매깁니다.
    private int remoteSequence;

    public bool IsConnected => running;

    public event Action<MatchMessage> MessageReceived;

    public event Action Connected;

    // 상대가 로컬 엔진이라 합의할 대상이 없습니다. 제어 메시지는 오지도 가지도 않습니다.
    public event Action<MatchControlMessage> ControlReceived;

    public void StartMatch(PieceColor color)
    {
        localColor = color;
        running = true;
        requestedTurn = -1;
        remoteSequence = 0;
        Debug.Log($"[Loopback] 매치 시작. 로컬 진영: {localColor}");

        // 기다릴 상대가 없으므로 곧바로 이어진 것으로 봅니다.
        Connected?.Invoke();
    }

    public void StopMatch()
    {
        running = false;
        requestedTurn = -1;
        MessageReceived = null;
        ControlReceived = null;
        Connected = null;
    }

    /// <summary>합의할 상대가 없으므로 아무것도 하지 않습니다.</summary>
    public void SendControl(MatchControlMessage message)
    {
        if (!running || message == null) return;

        Debug.Log($"[Loopback] 제어 메시지는 보낼 곳이 없어 무시합니다: {message}");
    }

    public void Send(MatchMessage message)
    {
        if (!running) return;

        // 루프백에는 실제로 받을 상대가 없으므로 흐름 확인용 로그만 남깁니다.
        Debug.Log($"[Loopback] 송신 {message}");
    }

    // 엔진 콜백이 알아서 도착하므로 프레임마다 펌핑할 것이 없습니다.
    public void Tick() { }

    public void NotifyRemoteTurnStarted(int turn)
    {
        if (!running) return;

        // 같은 턴에 요청이 두 번 들어오면 무시합니다.
        if (requestedTurn == turn) return;
        requestedTurn = turn;

        FairyStockfishBridge.Instance.GetBestMoveAsync(
            depth: AnalysisDepth,
            moveTimeMs: MoveTimeMs,
            callback: uci =>
            {
                if (!running) return;

                // 응답을 기다리는 사이 턴이 바뀌었으면 늦게 도착한 수이므로 버립니다.
                if (requestedTurn != turn) return;

                MatchMessage message = MatchMessage.CreateMove(turn, uci);
                message.Sequence = ++remoteSequence;

                Debug.Log($"[Loopback] 수신 {message}");
                MessageReceived?.Invoke(message);
            });
    }
}
