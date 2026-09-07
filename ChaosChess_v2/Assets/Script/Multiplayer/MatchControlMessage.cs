using System;

/// <summary>
/// 전송 프레임의 첫 바이트에 실리는 채널 구분자입니다.
///
/// 행동(MatchMessage)과 제어(MatchControlMessage)는 수신자도 다르고 순서 체계도 다릅니다.
/// 행동은 RemoteTurnProvider가 Sequence 기반 중복 필터를 태워 받고,
/// 제어는 MatchSession이 핸드셰이크로 받습니다. 그래서 한 타입에 섞지 않고 채널로 가릅니다.
/// </summary>
public static class MatchChannel
{
    /// <summary>한 턴의 행동 — <see cref="MatchMessage"/></summary>
    public const byte Action = 0;

    /// <summary>매치 시작 합의 — <see cref="MatchControlMessage"/></summary>
    public const byte Control = 1;
}

/// <summary>제어 메시지의 종류입니다.</summary>
public enum MatchControlKind
{
    /// <summary>호스트가 정한 초기 상태를 게스트에게 내려보냅니다.</summary>
    Setup = 0,

    /// <summary>게스트가 초기 상태를 검증한 결과를 돌려줍니다.</summary>
    SetupAck = 1
}

/// <summary>
/// 매치 시작 전에 오가는 제어 메시지입니다.
///
/// 행동 메시지와 달리 대국 중에는 흐르지 않습니다. 핸드셰이크가 끝나면 역할이 끝납니다.
/// </summary>
[Serializable]
public class MatchControlMessage
{
    public MatchControlKind Kind;

    /// <summary>Kind가 Setup일 때 합의할 초기 상태입니다.</summary>
    public MatchSetup Setup;

    /// <summary>Kind가 SetupAck일 때 게스트가 받아들였는지 여부입니다.</summary>
    public bool Accepted;

    /// <summary>거부했다면 그 사유입니다. 받아들였으면 비어 있습니다.</summary>
    public string Reason;

    /// <summary>
    /// 보내는 쪽의 표시용 프로필입니다. Setup과 SetupAck 양쪽에 실어 왕복 한 번으로 서로 교환합니다.
    ///
    /// <see cref="Setup"/>과 달리 <b>달라야 정상</b>이므로 검증 대상이 아닙니다.
    /// 그래서 MatchSetup에 넣지 않고 따로 둡니다.
    /// </summary>
    public MatchProfile Profile;

    public static MatchControlMessage CreateSetup(MatchSetup setup, MatchProfile profile)
    {
        return new MatchControlMessage
        {
            Kind = MatchControlKind.Setup,
            Setup = setup,
            Profile = profile
        };
    }

    public static MatchControlMessage CreateAck(bool accepted, string reason, MatchProfile profile)
    {
        return new MatchControlMessage
        {
            Kind = MatchControlKind.SetupAck,
            Accepted = accepted,
            Reason = reason,
            Profile = profile
        };
    }

    public override string ToString()
    {
        return Kind == MatchControlKind.Setup
            ? $"Setup({Setup})"
            : $"SetupAck({(Accepted ? "수락" : $"거부: {Reason}")})";
    }
}
