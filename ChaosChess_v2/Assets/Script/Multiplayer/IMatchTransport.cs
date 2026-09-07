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

    /// <summary>
    /// 상대와 이어진 순간 한 번 발행됩니다. 핸드셰이크를 시작할 시점 신호입니다.
    ///
    /// IsConnected를 폴링해도 되지만 연결 성립은 한 번뿐인 사건이라 이벤트가 맞습니다.
    /// MessageReceived와 같이 메인 스레드에서 발행해야 합니다.
    /// </summary>
    event Action Connected;

    /// <summary>제어 메시지가 도착했을 때 발행됩니다. 받는 쪽은 MatchSession입니다.</summary>
    event Action<MatchControlMessage> ControlReceived;

    /// <summary>
    /// 제어 메시지를 상대에게 보냅니다.
    ///
    /// 행동(Send)과 달리 대국 중에는 흐르지 않고, 순서 번호도 매기지 않습니다.
    /// 합의할 상대가 없는 구현(루프백)은 비워두면 됩니다.
    /// </summary>
    void SendControl(MatchControlMessage message);

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

    /// <summary>
    /// 매 프레임 호출됩니다. 소유자(MatchSession)가 Update에서 돌려줍니다.
    /// 매치 씬 밖에서도 수신이 멈추면 안 되므로 씬 오브젝트가 아니라 세션이 돌립니다.
    ///
    /// UnityTransport처럼 프레임마다 드라이버를 돌리고 수신 이벤트를 꺼내야 하는 구현을 위한 자리입니다.
    /// 펌핑이 필요 없는 구현(루프백 등)은 비워두면 됩니다.
    /// </summary>
    void Tick();
}
