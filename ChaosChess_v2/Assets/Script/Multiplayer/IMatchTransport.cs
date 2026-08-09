using System;

/// <summary>
/// 매치 메시지를 상대에게 전달하는 통로입니다.
///
/// 구현체를 갈아끼워도 게임 로직은 바뀌지 않도록, 게임 쪽은 이 인터페이스만 알아야 합니다.
/// 지금은 로컬 검증용 LoopbackMatchTransport만 있고, 나중에 UGS Relay 구현이 같은 자리에 들어갑니다.
/// </summary>
public interface IMatchTransport
{
    /// <summary>상대와 연결되어 메시지를 주고받을 수 있는 상태인지 여부입니다.</summary>
    bool IsConnected { get; }

    /// <summary>상대로부터 메시지가 도착했을 때 발행됩니다. 반드시 메인 스레드에서 발행해야 합니다.</summary>
    event Action<MatchMessage> MessageReceived;

    /// <summary>매치를 시작합니다.</summary>
    /// <param name="localColor">이 클라이언트가 맡은 진영</param>
    void StartMatch(PieceColor localColor);

    /// <summary>매치를 끝내고 자원을 정리합니다.</summary>
    void StopMatch();

    /// <summary>내 행동을 상대에게 보냅니다.</summary>
    void Send(MatchMessage message);

    /// <summary>
    /// 상대 차례가 시작됐음을 알립니다.
    ///
    /// 실제 전송 구현은 이 시점부터 응답 타임아웃을 재면 되고,
    /// 루프백 구현은 이 신호를 받아 상대 수를 만들어냅니다.
    /// </summary>
    void NotifyRemoteTurnStarted(int turn);
}
