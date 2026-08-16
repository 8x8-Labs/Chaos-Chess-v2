/// <summary>
/// 매치 메시지를 실어 나를 전송 계층의 종류입니다.
///
/// RemoteTurnProvider가 인스펙터에서 이 값을 받아 구현체를 고릅니다.
/// IMatchTransport가 인터페이스라 [SerializeField]로 직접 직렬화할 수 없기 때문에,
/// 종류만 직렬화하고 생성은 코드에서 합니다.
/// </summary>
public enum MatchTransportKind
{
    /// <summary>네트워크 없이 Fairy Stockfish가 상대역을 맡습니다. 흐름 검증용입니다.</summary>
    Loopback = 0,

    /// <summary>UGS Relay를 통해 실제 상대와 연결합니다.</summary>
    Relay = 1
}
